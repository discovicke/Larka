using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Elevens studieplan: kurserna hen läser som elev, med poäng, status och betyg på uppgifterna.
// Bara det eleven själv ser i elevvyn räknas: publicerat innehåll i publicerade veckor.
public class StudyPlanService(
    IGroupRepository groups,
    IContentRepository contents,
    IModuleRepository modules,
    ISubmissionRepository submissions,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public async Task<StudyPlanDto> GetAsync(CancellationToken ct) =>
        await GetForStudentAsync(currentUser.UserId, null, ct);

    /// <summary>
    /// Studieplanen för en elev. Med onlyGroupId räknas bara kurserna som just den gruppen läser, med
    /// gruppens egna länkar. Behörigheten prövas av den som anropar.
    /// </summary>
    public async Task<StudyPlanDto> GetForStudentAsync(Guid studentId, Guid? onlyGroupId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var studentCourses = (await groups.GetStudentCoursesAsync(studentId, ct))
            .Where(studentCourse => onlyGroupId is null || studentCourse.GroupIds.Contains(onlyGroupId.Value))
            .Select(studentCourse => onlyGroupId is null
                ? studentCourse
                : studentCourse with { GroupIds = [onlyGroupId.Value] })
            .ToList();
        var ordered = studentCourses
            .OrderBy(studentCourse => studentCourse.Course.StartsOn is null
                ? 1
                : 0)
            .ThenBy(studentCourse => studentCourse.Course.StartsOn)
            .ThenBy(studentCourse => studentCourse.Course.Title)
            .ToList();

        var courseDtos = new List<StudyPlanCourseDto>();
        foreach (var studentCourse in ordered)
        {
            courseDtos.Add(await ToDtoAsync(studentCourse, studentId, now, today, ct));
        }

        var completedPoints = courseDtos
            .Where(course => course.Status == StudyPlanCourseStatus.Completed)
            .Sum(course => course.Points ?? 0);
        var totalPoints = courseDtos.Sum(course => course.Points ?? 0);

        return new StudyPlanDto(completedPoints, totalPoints, courseDtos);
    }

    private async Task<StudyPlanCourseDto> ToDtoAsync(
        StudentCourse studentCourse, Guid studentId, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var course = studentCourse.Course;
        var courseModules = await modules.GetForCourseAsync(course.Id, ct);
        var availableModuleIds = courseModules
            .Where(module => module.PublishAt is null || module.PublishAt <= now)
            .Select(module => module.Id)
            .ToHashSet();

        var courseLinks = new List<LinkedContent>();
        foreach (var groupId in studentCourse.GroupIds)
        {
            courseLinks.AddRange(await contents.GetLinksWithContentForGroupCourseAsync(groupId, course.Id, ct));
        }

        var assignments = courseLinks
            .Where(link => link.Link.ModuleId is not null
                && availableModuleIds.Contains(link.Link.ModuleId.Value)
                && IsVisible(link, now)
                && IsSubmissionAssignment(link.Content))
            .DistinctBy(link => link.Link.Id)
            .OrderBy(link => link.Link.Position)
            .ThenBy(link => link.Link.PublishAt)
            .ThenBy(link => link.Link.Id)
            .ToList();

        var linkIds = assignments.Select(link => link.Link.Id).ToList();
        var studentSubmissions = await submissions.GetForStudentAsync(studentId, linkIds, ct);
        var submissionByLinkId = studentSubmissions.ToDictionary(submission => submission.ContentLinkId);
        var assignmentDtos = assignments
            .Select(link => ToAssignmentDto(link, submissionByLinkId.GetValueOrDefault(link.Link.Id)))
            .ToList();

        return new StudyPlanCourseDto(
            course.Id,
            course.Title,
            course.Code,
            course.Points,
            course.StartsOn,
            course.EndsOn,
            StatusOf(course, today),
            null,
            assignmentDtos.Count,
            assignmentDtos.Count(assignment => assignment.Status != SubmissionStatus.NotStarted),
            assignmentDtos.Count(assignment => assignment.Status == SubmissionStatus.Graded),
            assignmentDtos);
    }

    private static StudyPlanAssignmentDto ToAssignmentDto(LinkedContent link, Submission? submission)
    {
        var feedback = submission?.Grade?.Feedback;
        return new StudyPlanAssignmentDto(
            link.Link.Id,
            link.Content.Title,
            link.Link.DueAt,
            link.Link.ResubmitDueAt,
            submission?.PersonalDueAt,
            submission?.Status ?? SubmissionStatus.NotStarted,
            submission?.Grade?.Level,
            string.IsNullOrWhiteSpace(feedback)
                ? null
                : feedback,
            submission?.Grade?.GradedAt);
    }

    private static StudyPlanCourseStatus StatusOf(Course course, DateOnly today)
    {
        if (course.EndsOn is not null && course.EndsOn.Value < today)
        {
            return StudyPlanCourseStatus.Completed;
        }

        if (course.StartsOn is not null && course.StartsOn.Value > today)
        {
            return StudyPlanCourseStatus.Upcoming;
        }

        return StudyPlanCourseStatus.Ongoing;
    }

    // Samma synlighet som elevvyn: bara publicerat innehåll som publicerats.
    private static bool IsVisible(LinkedContent link, DateTimeOffset now) =>
        link.Content.Status == ContentStatus.Published && link.Link.PublishAt <= now;

    // Bara uppgifter med ett inlämningssätt är inlämningar. En övning utan inlämning räknas inte.
    private static bool IsSubmissionAssignment(Content content) =>
        content.Type == ContentType.Assignment
        && content.Assignment is not null
        && content.Assignment.SubmissionModes.Count > 0;
}
