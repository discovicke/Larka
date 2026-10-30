using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Backend.Application.Services;

/// <summary>
/// Beräknar lärarens stödsignaler och hanterar elevens egna "Jag behöver hjälp"-flagga.
/// </summary>
public class StudentSupportService(
    IStudentHelpRequestRepository helpRequests,
    ISubmissionRepository submissions,
    IContentRepository contents,
    IGroupRepository groups,
    IPermissions permissions,
    NotificationService notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IOptions<GitHubSupportOptions> options,
    TimeProvider time)
{
    public async Task<Dictionary<Guid, StudentSupportDto>> BuildAsync(
        IReadOnlyCollection<Submission> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var settings = options.Value;
        var linkIds = items.Select(item => item.ContentLinkId).Distinct().ToList();
        var requests = await helpRequests.GetForLinksAsync(linkIds, ct);
        var requestsByKey = requests.ToDictionary(
            request => (request.ContentLinkId, request.StudentId));

        return items.ToDictionary(
            item => item.Id,
            item => BuildOne(
                item,
                requestsByKey.GetValueOrDefault((item.ContentLinkId, item.StudentId)),
                now,
                settings));
    }

    public async Task<ServiceResult<StudentHelpStatusDto>> GetHelpAsync(
        Guid linkId, CancellationToken ct)
    {
        var linked = await AccessibleLinkAsync(linkId, ct);
        if (linked is null)
        {
            return ServiceResult<StudentHelpStatusDto>.NotFound("assignment not found");
        }

        var request = await helpRequests.GetAsync(linkId, currentUser.UserId, ct);
        return ServiceResult<StudentHelpStatusDto>.Ok(ToStatus(request));
    }

    public async Task<ServiceResult<StudentHelpStatusDto>> SetHelpAsync(
        Guid linkId, SetStudentHelpDto dto, CancellationToken ct)
    {
        var linked = await AccessibleLinkAsync(linkId, ct);
        if (linked is null)
        {
            return ServiceResult<StudentHelpStatusDto>.NotFound("assignment not found");
        }

        var now = time.GetUtcNow();
        var request = await helpRequests.GetAsync(linkId, currentUser.UserId, ct);
        var notify = request is null || request.ResolvedAt is not null;
        if (request is null)
        {
            request = new StudentHelpRequest
            {
                StudentId = currentUser.UserId,
                ContentLinkId = linkId,
                Message = CleanMessage(dto.Message),
                CreatedAt = now,
                UpdatedAt = now
            };
            helpRequests.Add(request);
        }
        else
        {
            request.Message = CleanMessage(dto.Message);
            request.UpdatedAt = now;
            request.ResolvedAt = null;
            request.ResolvedById = null;
        }

        if (notify)
        {
            var submission = await submissions.GetForStudentAsync(linkId, currentUser.UserId, ct);
            await notifications.NotifyStudentHelpRequestedAsync(
                linked.Link, linked.Content.Id, submission?.Id, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<StudentHelpStatusDto>.Ok(ToStatus(request));
    }

    public async Task<ServiceResult<bool>> WithdrawHelpAsync(Guid linkId, CancellationToken ct)
    {
        var linked = await AccessibleLinkAsync(linkId, ct);
        if (linked is null)
        {
            return ServiceResult<bool>.NotFound("assignment not found");
        }

        var request = await helpRequests.GetAsync(linkId, currentUser.UserId, ct);
        if (request is null || request.ResolvedAt is not null)
        {
            return ServiceResult<bool>.Ok(true);
        }

        request.ResolvedAt = time.GetUtcNow();
        request.ResolvedById = currentUser.UserId;
        request.UpdatedAt = request.ResolvedAt.Value;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> ResolveHelpAsync(
        Guid linkId, Guid studentId, CancellationToken ct)
    {
        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var request = await helpRequests.GetAsync(linkId, studentId, ct);
        if (request is null)
        {
            return ServiceResult<bool>.NotFound("help request not found");
        }

        if (request.ResolvedAt is null)
        {
            request.ResolvedAt = time.GetUtcNow();
            request.ResolvedById = currentUser.UserId;
            request.UpdatedAt = request.ResolvedAt.Value;
            await unitOfWork.SaveChangesAsync(ct);
        }

        return ServiceResult<bool>.Ok(true);
    }

    private static StudentSupportDto BuildOne(
        Submission submission,
        StudentHelpRequest? request,
        DateTimeOffset now,
        GitHubSupportOptions settings)
    {
        var repo = submission.Repo;
        var started = repo is not null
            && (repo.InitialSha is not null
                ? repo.LastSha != repo.InitialSha
                : repo.CommitCount > 1);
        var notStarted = repo is not null
            && repo.ProvisioningState == RepoProvisioningState.Ready
            && !started
            && (repo.ProvisionedAt ?? repo.UpdatedAt) <= now.AddDays(-settings.NotStartedAfterDays);
        var noRecentPush = repo is not null
            && repo.ProvisioningState == RepoProvisioningState.Ready
            && started
            && submission.Status is SubmissionStatus.NotStarted or SubmissionStatus.NeedsWork
            && (repo.LastCommitAt ?? repo.UpdatedAt) <= now.AddDays(-settings.NoPushAfterDays);
        var redChecks = repo is not null
            && repo.ProvisioningState == RepoProvisioningState.Ready
            && repo.CheckStatus == CheckStatus.Red
            && (repo.CheckStatusChangedAt ?? repo.LastCommitAt ?? repo.UpdatedAt)
                <= now.AddHours(-settings.RedChecksAfterHours);
        var helpRequested = request is not null && request.ResolvedAt is null;

        return new StudentSupportDto(
            notStarted,
            noRecentPush,
            redChecks,
            helpRequested,
            request?.CreatedAt,
            request?.Message,
            request?.ResolvedAt is not null);
    }

    private async Task<LinkedContent?> AccessibleLinkAsync(Guid linkId, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(linkId, ct);
        if (linked is null)
        {
            return null;
        }

        var studentGroups = await groups.GetStudentGroupIdsAsync(currentUser.UserId, ct);
        if (linked.Link.GroupId is not null && !studentGroups.Contains(linked.Link.GroupId.Value))
        {
            return null;
        }

        if (linked.Link.CourseId is not null)
        {
            var reading = await groups.GetGroupIdsReadingCourseAsync(linked.Link.CourseId.Value, ct);
            if (!reading.Any(studentGroups.Contains))
            {
                return null;
            }
        }

        return linked;
    }

    private static string? CleanMessage(string? message) =>
        string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();

    private static StudentHelpStatusDto ToStatus(StudentHelpRequest? request) =>
        request is null
            ? new StudentHelpStatusDto(false, null, null, false)
            : new StudentHelpStatusDto(
                request.ResolvedAt is null,
                request.Message,
                request.CreatedAt,
                request.ResolvedAt is not null);
}
