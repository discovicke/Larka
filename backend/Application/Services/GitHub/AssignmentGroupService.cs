using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

/// <summary>
/// Läser och ändrar en gruppindelning efter att uppgiften delats ut. Befintliga grupper behålls,
/// men läraren kan byta namn och flytta elever mellan dem. GitHub-collaborators syncas per repo.
/// </summary>
public class AssignmentGroupService(
    IContentRepository contents,
    IGroupRepository groups,
    IUserRepository users,
    ISubmissionRepository submissions,
    ILabProvisioner provisioner,
    IPermissions permissions,
    IUnitOfWork unitOfWork,
    ILogger<AssignmentGroupService> logger)
{
    public async Task<ServiceResult<List<AssignmentGroupDto>>> GetAsync(
        Guid linkId, CancellationToken ct)
    {
        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<List<AssignmentGroupDto>>.Forbidden();
        }

        var link = await contents.GetLinkWithContentAsync(linkId, ct);
        if (link is null)
        {
            return ServiceResult<List<AssignmentGroupDto>>.NotFound("link not found");
        }

        var groupsForLink = await contents.GetGroupsForLinkAsync(linkId, ct);
        return ServiceResult<List<AssignmentGroupDto>>.Ok(
            groupsForLink.Select(AssignmentGroupDto.From).ToList());
    }

    public async Task<ServiceResult<List<AssignmentGroupDto>>> UpdateAsync(
        Guid linkId, UpdateAssignmentGroupsDto dto, CancellationToken ct)
    {
        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<List<AssignmentGroupDto>>.Forbidden();
        }

        var linked = await contents.GetLinkWithContentAsync(linkId, ct);
        if (linked is null)
        {
            return ServiceResult<List<AssignmentGroupDto>>.NotFound("link not found");
        }

        if (linked.Content.Assignment?.WorkMode != RepoWorkMode.Group)
        {
            return ServiceResult<List<AssignmentGroupDto>>.Invalid(
                "the link is not a group assignment");
        }

        var existing = await contents.GetGroupsForLinkAsync(linkId, ct);
        var byId = existing.ToDictionary(group => group.Id);
        var inputGroups = dto.Groups ?? [];
        var inputIds = inputGroups.Select(group => group.Id).ToList();
        if (inputIds.Any(id => id is null)
            || inputIds.Distinct().Count() != inputIds.Count
            || !inputIds.Select(id => id!.Value).ToHashSet().SetEquals(byId.Keys))
        {
            return ServiceResult<List<AssignmentGroupDto>>.Invalid(
                "groups can be renamed and rearranged, but not added or removed after handout");
        }

        var validationError = await AssignmentGroupPolicy.ValidateAsync(
            RepoWorkMode.Group,
            linked.Link.GroupId,
            linked.Link.CourseId,
            inputGroups,
            groups,
            users,
            ct);
        if (validationError is not null)
        {
            return ServiceResult<List<AssignmentGroupDto>>.Invalid(validationError);
        }

        var groupIds = linked.Link.GroupId is not null
            ? [linked.Link.GroupId.Value]
            : await groups.GetGroupIdsReadingCourseAsync(linked.Link.CourseId!.Value, ct);
        var students = (await users.GetStudentsInGroupsAsync(groupIds, ct))
            .ToDictionary(student => student.Id);
        var linkSubmissions = await submissions.GetByLinkAsync(linkId, ct);
        var oldGroupByStudent = linkSubmissions
            .ToDictionary(submission => submission.StudentId, submission => submission.AssignmentGroupId);
        var newGroupByStudent = inputGroups
            .SelectMany(group => group.StudentIds.Select(studentId => (studentId, group.Id!.Value)))
            .ToDictionary(entry => entry.studentId, entry => entry.Value);

        var moved = linkSubmissions
            .Where(submission => oldGroupByStudent[submission.StudentId] != newGroupByStudent[submission.StudentId])
            .ToList();
        var alreadyWorked = moved.FirstOrDefault(submission =>
            submission.Status is SubmissionStatus.Submitted
                or SubmissionStatus.NeedsWork
                or SubmissionStatus.Graded);
        if (alreadyWorked is not null)
        {
            return ServiceResult<List<AssignmentGroupDto>>.Invalid(
                "a student who has already submitted or been graded cannot change group");
        }

        var position = 0;
        foreach (var input in inputGroups)
        {
            var group = byId[input.Id!.Value];
            group.Name = input.Name.Trim();
            group.Position = ++position;
            group.Members.Clear();
            foreach (var studentId in input.StudentIds)
            {
                group.Members.Add(new AssignmentGroupMember
                {
                    AssignmentGroupId = group.Id,
                    StudentId = studentId,
                    User = students[studentId],
                });
            }
        }

        foreach (var submission in linkSubmissions)
        {
            submission.AssignmentGroupId = newGroupByStudent[submission.StudentId];
        }

        // Flytta collaborator-åtkomsten innan grupprepot kan byta ledare.
        foreach (var submission in moved)
        {
            var oldGroupId = oldGroupByStudent[submission.StudentId];
            var oldGroup = oldGroupId is not null
                ? byId[oldGroupId.Value]
                : null;
            var newGroup = byId[newGroupByStudent[submission.StudentId]];
            var student = students[submission.StudentId];
            await SyncCollaboratorAsync(
                oldGroup?.Repo, newGroup.Repo, student, oldGroup?.Id != newGroup.Id, ct);
        }

        // Om gruppens ledare byter grupp måste repot flyttas till en kvarvarande medlem.
        foreach (var group in existing)
        {
            if (group.Repo is null)
            {
                continue;
            }

            if (moved.All(submission => submission.Id != group.Repo.SubmissionId))
            {
                continue;
            }

            var remaining = linkSubmissions.FirstOrDefault(submission =>
                submission.AssignmentGroupId == group.Id);
            if (remaining is null)
            {
                return ServiceResult<List<AssignmentGroupDto>>.Invalid(
                    "a group cannot be empty when it has a repository");
            }

            await submissions.ReassignGroupRepoAsync(group.Id, remaining.Id, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<List<AssignmentGroupDto>>.Ok(
            existing.OrderBy(group => group.Position)
                .Select(AssignmentGroupDto.From)
                .ToList());
    }

    private async Task SyncCollaboratorAsync(
        StudentRepo? oldRepo,
        StudentRepo? newRepo,
        User student,
        bool changedGroup,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(student.GitHubLogin))
        {
            return;
        }

        if (changedGroup && oldRepo is not null)
        {
            try
            {
                await provisioner.RemoveRepoCollaboratorAsync(
                    oldRepo, student.SchoolId, student, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not remove {GitHubLogin} from old group repo {Repo}",
                    student.GitHubLogin,
                    oldRepo.RepoFullName);
            }
        }

        if (newRepo is not null)
        {
            try
            {
                await provisioner.AddRepoCollaboratorAsync(
                    newRepo, student.SchoolId, student, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not add {GitHubLogin} to new group repo {Repo}",
                    student.GitHubLogin,
                    newRepo.RepoFullName);
            }
        }
    }
}
