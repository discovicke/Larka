using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

/// <summary>Validering som delas av skapande och senare ändring av en gruppindelning.</summary>
public static class AssignmentGroupPolicy
{
    public static async Task<string?> ValidateAsync(
        RepoWorkMode workMode,
        Guid? groupId,
        Guid? courseId,
        IReadOnlyList<AssignmentGroupInput>? groups,
        IGroupRepository groupRepository,
        IUserRepository users,
        CancellationToken ct)
    {
        if (workMode != RepoWorkMode.Group)
        {
            return groups is not null && groups.Count > 0
                ? "groups are only allowed on group repo assignments"
                : null;
        }

        if (groups is null || groups.Count == 0)
        {
            return "groups are required on group repo assignments";
        }

        var names = groups.Select(group => group.Name.Trim()).ToList();
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            return "every group needs a name";
        }

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        {
            return "group names must be unique";
        }

        if (groups.Any(group => group.StudentIds is null || group.StudentIds.Count == 0))
        {
            return "every group needs at least one student";
        }

        var studentIds = groups.SelectMany(group => group.StudentIds).ToList();
        if (studentIds.Distinct().Count() != studentIds.Count)
        {
            return "a student can only belong to one group";
        }

        var eligibleGroupIds = groupId is not null
            ? [groupId.Value]
            : await groupRepository.GetGroupIdsReadingCourseAsync(courseId!.Value, ct);
        var eligible = (await users.GetStudentsInGroupsAsync(eligibleGroupIds, ct))
            .Select(student => student.Id)
            .ToHashSet();
        if (studentIds.Any(studentId => !eligible.Contains(studentId)))
        {
            return "a group contains a student who does not read the course";
        }

        return eligible.Except(studentIds).Any()
            ? "every student in the course must belong to a group"
            : null;
    }
}
