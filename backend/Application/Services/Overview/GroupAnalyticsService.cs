using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Utbildningsledarens klassöversikt. Bara admin, och bara den egna skolan. Räknar bara uppgifter som
// eleven kan lämna in, i publicerade veckor och med publicerat innehåll, som elevvyn.
public class GroupAnalyticsService(
    IGroupRepository groups,
    IModuleRepository modules,
    IContentRepository contents,
    ISubmissionRepository submissions,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    TimeProvider time)
{
    public async Task<ServiceResult<GroupAnalyticsDto>> GetAsync(Guid groupId, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GroupAnalyticsDto>.Forbidden();
        }

        var group = await groups.GetAsync(groupId, ct);
        if (group is null || !await currentSchool.OwnsAsync(group.SchoolId, ct))
        {
            return ServiceResult<GroupAnalyticsDto>.NotFound("group not found");
        }

        var students = (await groups.GetMembersAsync(groupId, ct))
            .Where(member => member.Role == GroupRole.Student)
            .OrderBy(member => member.Name)
            .ToList();
        var courses = (await groups.GetCoursesAsync(groupId, ct))
            .OrderByDescending(course => course.StartsOn ?? DateOnly.MinValue)
            .ThenBy(course => course.Title)
            .ToList();
        var now = time.GetUtcNow();
        var assignmentsByCourse = new Dictionary<Guid, List<AnalyticsAssignment>>();

        foreach (var course in courses)
        {
            var courseModules = await modules.GetForCourseAsync(course.Id, ct);
            var availableModuleIds = courseModules
                .Where(module => module.PublishAt is null || module.PublishAt <= now)
                .Select(module => module.Id)
                .ToHashSet();
            var links = await contents.GetLinksWithContentForGroupCourseAsync(groupId, course.Id, ct);
            assignmentsByCourse[course.Id] = links
                .Where(link => link.Link.ModuleId is not null
                    && availableModuleIds.Contains(link.Link.ModuleId.Value)
                    && link.Content.Status == ContentStatus.Published
                    && link.Link.PublishAt <= now
                    && link.Content.Type == ContentType.Assignment
                    && link.Content.Assignment is not null
                    && link.Content.Assignment.SubmissionModes.Count > 0)
                .DistinctBy(link => link.Link.Id)
                .Select(link => new AnalyticsAssignment(link.Link.Id, link.Link.DueAt))
                .ToList();
        }

        var studentIds = students.Select(student => student.UserId).ToList();
        var linkIds = assignmentsByCourse.Values
            .SelectMany(assignments => assignments.Select(assignment => assignment.LinkId))
            .Distinct()
            .ToList();
        var savedSubmissions = studentIds.Count == 0 || linkIds.Count == 0
            ? []
            : await submissions.GetForStudentsAsync(studentIds, linkIds, ct);
        var submissionsByStudentAndLink = savedSubmissions
            .ToDictionary(submission => (submission.StudentId, submission.ContentLinkId));

        var studentDtos = students
            .Select(student =>
            {
                var courseRows = courses
                    .Select(course => ToStudentCourse(
                        course.Id,
                        assignmentsByCourse[course.Id],
                        submissionsByStudentAndLink,
                        student.UserId,
                        now))
                    .ToList();
                return new GroupAnalyticsStudentDto(
                    student.UserId,
                    student.Name,
                    courseRows.Any(row => row.NeedsSupport),
                    courseRows);
            })
            .ToList();

        var courseDtos = courses
            .Select(course =>
            {
                var rows = studentDtos
                    .SelectMany(student => student.Courses)
                    .Where(row => row.CourseId == course.Id)
                    .ToList();
                return new GroupAnalyticsCourseDto(
                    course.Id,
                    course.Title,
                    course.Code,
                    course.StartsOn,
                    course.EndsOn,
                    students.Count,
                    assignmentsByCourse[course.Id].Count,
                    rows.Sum(row => row.GradedCount),
                    rows.Sum(row => row.FailCount),
                    rows.Sum(row => row.PassCount),
                    rows.Sum(row => row.DistinctionCount),
                    rows.Sum(row => row.NeedsWorkCount),
                    rows.Sum(row => row.SubmittedCount),
                    rows.Sum(row => row.OverdueCount),
                    rows.Sum(row => row.NotSubmittedCount),
                    rows.Count(row => row.NeedsSupport));
            })
            .ToList();

        return ServiceResult<GroupAnalyticsDto>.Ok(
            new GroupAnalyticsDto(group.Id, group.Name, courseDtos, studentDtos));
    }

    private static GroupAnalyticsStudentCourseDto ToStudentCourse(
        Guid courseId,
        IReadOnlyCollection<AnalyticsAssignment> assignments,
        IReadOnlyDictionary<(Guid StudentId, Guid LinkId), Submission> submissions,
        Guid studentId,
        DateTimeOffset now)
    {
        var graded = 0;
        var fail = 0;
        var pass = 0;
        var distinction = 0;
        var needsWork = 0;
        var submitted = 0;
        var overdue = 0;
        var notSubmitted = 0;

        foreach (var assignment in assignments)
        {
            submissions.TryGetValue((studentId, assignment.LinkId), out var submission);
            var status = submission?.Status ?? SubmissionStatus.NotStarted;
            switch (status)
            {
                case SubmissionStatus.Graded:
                    graded += 1;
                    switch (submission?.Grade?.Level)
                    {
                        case GradeLevel.Fail:
                            fail += 1;
                            break;
                        case GradeLevel.Pass:
                            pass += 1;
                            break;
                        case GradeLevel.Distinction:
                            distinction += 1;
                            break;
                    }

                    break;
                case SubmissionStatus.NeedsWork:
                    needsWork += 1;
                    break;
                case SubmissionStatus.Submitted:
                    submitted += 1;
                    break;
                case SubmissionStatus.NotSubmitted:
                    notSubmitted += 1;
                    break;
                case SubmissionStatus.NotStarted when assignment.DueAt is not null && assignment.DueAt < now:
                    overdue += 1;
                    break;
            }
        }

        return new GroupAnalyticsStudentCourseDto(
            courseId,
            assignments.Count,
            graded,
            fail,
            pass,
            distinction,
            needsWork,
            submitted,
            overdue,
            notSubmitted,
            fail > 0 || needsWork > 0 || overdue > 0 || notSubmitted > 0);
    }

    private sealed record AnalyticsAssignment(Guid LinkId, DateTimeOffset? DueAt);
}
