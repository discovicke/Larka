using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Lärarens Idag-sida. Bara kurserna där användaren är lärare (en rad i CourseTeachers) räknas, och bara deras
// inlämningar, deadlines och schemalagda publiceringar. Frågorna går över hela kursen, inte per rad.
public class TeachingService(
    IGroupRepository groups,
    ICourseRepository courses,
    IContentRepository contents,
    ISubmissionRepository submissions,
    ICurrentUser currentUser,
    TimeProvider time)
{
    private const int _toGradeLength = 8;
    private const int _deadlineDays = 14;
    private const int _scheduledDays = 7;

    public async Task<TeachingDto> GetAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var teachingCourseIds = (await groups.GetCourseRolesAsync(currentUser.UserId, ct))
            .Where(courseRole => courseRole.Role == GroupRole.Teacher)
            .Select(courseRole => courseRole.CourseId)
            .Distinct()
            .ToList();
        if (teachingCourseIds.Count == 0)
        {
            return new TeachingDto([], 0, [], [], []);
        }

        var courseList = (await courses.GetByIdsAsync(teachingCourseIds, ct))
            .OrderBy(course => course.Code)
            .ThenBy(course => course.Title)
            .ToList();
        var courseTitles = courseList.ToDictionary(course => course.Id, course => course.Title);
        var studentsByCourse = (await groups.GetStudentsByCourseAsync(teachingCourseIds, ct))
            .GroupBy(student => student.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(student => student.UserId).ToHashSet());

        var toGradeCounts = (await submissions.GetToGradeCountsAsync(teachingCourseIds, ct))
            .ToDictionary(count => count.CourseId, count => count.Count);
        var preparedCourseIds = await contents.GetCourseIdsWithPublishedPartsAsync(teachingCourseIds, ct);
        var toGrade = await submissions.GetToGradeAsync(teachingCourseIds, _toGradeLength, ct);

        return new TeachingDto(
            toGrade
                .Select(item => new TeachingToGradeDto(
                    item.SubmissionId,
                    item.LinkId,
                    item.Title,
                    item.CourseId,
                    courseTitles.GetValueOrDefault(item.CourseId) ?? "",
                    item.StudentId,
                    item.StudentName,
                    item.SubmittedAt))
                .ToList(),
            toGradeCounts.Values.Sum(),
            await GetDeadlinesAsync(teachingCourseIds, studentsByCourse, now, ct),
            await GetScheduledAsync(teachingCourseIds, now, ct),
            courseList
                .Select(course => new TeachingCourseDto(
                    course.Id,
                    course.Code,
                    course.Title,
                    studentsByCourse.GetValueOrDefault(course.Id)?.Count ?? 0,
                    toGradeCounts.GetValueOrDefault(course.Id),
                    NeedsPreparation(course, preparedCourseIds, now),
                    course.StartsOn,
                    course.EndsOn))
                .ToList());
    }

    // Beskrivningen är lärarens text. Saknas den, eller saknas publicerade delar i en kurs som ännu inte har börjat,
    // är kursen inte förberedd.
    private static bool NeedsPreparation(Course course, HashSet<Guid> preparedCourseIds, DateTimeOffset now)
    {
        var upcoming = course.StartsOn is not null && course.StartsOn.Value > DateOnly.FromDateTime(now.UtcDateTime);
        return string.IsNullOrWhiteSpace(course.Description)
            || (upcoming && !preparedCourseIds.Contains(course.Id));
    }

    // Deadlinen räknas bland eleverna som fortfarande läser kursen, så att antalet aldrig överstiger dem.
    private async Task<List<TeachingDeadlineDto>> GetDeadlinesAsync(
        List<Guid> teachingCourseIds,
        Dictionary<Guid, HashSet<Guid>> studentsByCourse,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var links = await contents.GetCourseDeadlinesAsync(
            teachingCourseIds, now, now.AddDays(_deadlineDays), ct);
        var submittedByLink = (await submissions.GetActiveForLinksAsync(
                links.Select(link => link.LinkId).ToList(), ct))
            .GroupBy(submission => submission.LinkId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(submission => submission.StudentId).ToHashSet());

        return links
            .Select(link =>
            {
                var students = studentsByCourse.GetValueOrDefault(link.CourseId) ?? [];
                var submitted = submittedByLink.TryGetValue(link.LinkId, out var submittedStudents)
                    ? submittedStudents.Count(students.Contains)
                    : 0;
                return new TeachingDeadlineDto(
                    link.LinkId,
                    link.Title,
                    link.CourseId,
                    link.CourseTitle,
                    link.DueAt,
                    submitted,
                    students.Count);
            })
            .ToList();
    }

    private async Task<List<TeachingScheduledDto>> GetScheduledAsync(
        List<Guid> teachingCourseIds, DateTimeOffset now, CancellationToken ct)
    {
        var to = now.AddDays(_scheduledDays);
        var links = await contents.GetScheduledLinksAsync(teachingCourseIds, now, to, ct);
        var modules = await contents.GetScheduledModulesAsync(teachingCourseIds, now, to, ct);

        return links
            .Select(link => new TeachingScheduledDto(
                link.LinkId,
                null,
                link.Title,
                link.CourseId,
                link.CourseTitle,
                link.PublishAt,
                "Part"))
            .Concat(modules.Select(module => new TeachingScheduledDto(
                null,
                module.ModuleId,
                module.Title,
                module.CourseId,
                module.CourseTitle,
                module.PublishAt,
                "Week")))
            .OrderBy(item => item.PublishAt)
            .ThenBy(item => item.Title)
            .ToList();
    }
}
