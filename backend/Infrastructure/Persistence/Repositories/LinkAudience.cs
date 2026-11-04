using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

// Den enda regeln för "elever en del delas ut till", så att veckolistan och Idag räknar likadant.
internal static class LinkAudience
{
    // Unika elever per utdelning. Nämnaren är eleverna i länkens grupp, eller i alla grupper som läser kursen när
    // länken gäller hela kursen. Avaktiverade elever räknas inte, på samma sätt som på klasssidan, och en
    // inlämning räknas bara om eleven fortfarande går i en av grupperna.
    public static async Task<Dictionary<Guid, (int Submitted, int Students)>> CountAsync(
        AppDb db, IReadOnlyCollection<ContentLink> links, CancellationToken ct)
    {
        if (links.Count == 0)
        {
            return [];
        }

        var courseIds = links
            .Where(link => link.GroupId is null && link.CourseId is not null)
            .Select(link => link.CourseId!.Value)
            .Distinct()
            .ToList();
        var groupIdsByCourse = (await db.GroupCourses
                .Where(groupCourse => courseIds.Contains(groupCourse.CourseId))
                .Select(groupCourse => new { groupCourse.CourseId, groupCourse.GroupId })
                .ToListAsync(ct))
            .GroupBy(groupCourse => groupCourse.CourseId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.GroupId).ToList());
        var linkGroupIds = links
            .Where(link => link.GroupId is not null)
            .Select(link => link.GroupId!.Value)
            .Distinct()
            .ToList();
        var allGroupIds = groupIdsByCourse.Values
            .SelectMany(ids => ids)
            .Concat(linkGroupIds)
            .Distinct()
            .ToList();
        var students = await (from enrollment in db.Enrollments
                              join user in db.Users on enrollment.UserId equals user.Id
                              where enrollment.Role == GroupRole.Student
                                  && user.DeactivatedAt == null
                                  && allGroupIds.Contains(enrollment.GroupId)
                              select new { enrollment.GroupId, enrollment.UserId })
            .ToListAsync(ct);
        var studentsByGroup = students
            .GroupBy(student => student.GroupId)
            .ToDictionary(group => group.Key, group => group.Select(student => student.UserId).ToHashSet());
        var linkIds = links.Select(link => link.Id).ToList();
        var submitted = (await db.Submissions
                .Where(submission => linkIds.Contains(submission.ContentLinkId)
                    && submission.Status != SubmissionStatus.NotStarted)
                .Select(submission => new { submission.ContentLinkId, submission.StudentId })
                .Distinct()
                .ToListAsync(ct))
            .ToLookup(submission => submission.ContentLinkId, submission => submission.StudentId);

        return links.ToDictionary(
            link => link.Id,
            link =>
            {
                var groupIds = link.GroupId is not null
                    ? [link.GroupId.Value]
                    : groupIdsByCourse.GetValueOrDefault(link.CourseId ?? Guid.Empty) ?? [];
                var linkStudents = groupIds
                    .SelectMany(id => studentsByGroup.GetValueOrDefault(id) ?? [])
                    .ToHashSet();
                return (submitted[link.Id].Count(linkStudents.Contains), linkStudents.Count);
            });
    }
}
