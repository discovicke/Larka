using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class TeachingEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Setup(
        Guid GroupId,
        Guid OtherGroupId,
        Guid CourseId,
        Guid OtherCourseId,
        Guid StudentId,
        Guid OtherStudentId,
        Guid OtherGroupStudentId,
        Guid TeacherId);

    private record TeachingResponse(
        List<ToGradeResponse> ToGrade,
        int ToGradeTotal,
        List<DeadlineResponse> Deadlines,
        List<ScheduledResponse> Scheduled,
        List<CourseResponse> Courses);

    private record ToGradeResponse(
        Guid SubmissionId,
        Guid LinkId,
        string Title,
        Guid CourseId,
        string CourseTitle,
        Guid StudentId,
        string StudentName,
        DateTimeOffset SubmittedAt);

    private record DeadlineResponse(
        Guid LinkId,
        string Title,
        Guid CourseId,
        string CourseTitle,
        DateTimeOffset DueAt,
        int Submitted,
        int Students);

    private record ScheduledResponse(
        Guid? LinkId,
        Guid? ModuleId,
        string Title,
        Guid CourseId,
        string CourseTitle,
        DateTimeOffset PublishAt,
        string Kind);

    private record CourseResponse(
        Guid CourseId, string Code, string Title, int Students, int ToGrade, bool NeedsPreparation,
        DateOnly? StartsOn, DateOnly? EndsOn);

    [Fact]
    public async Task NeedsPreparation_IsTrue_WhenTheDescriptionIsEmpty()
    {
        var setup = await AddSetupAsync();

        var teaching = await GetTeachingAsync(setup.TeacherId);

        Assert.True(teaching.Courses.Single().NeedsPreparation);
    }

    [Fact]
    public async Task Courses_CarryTheCourseDates()
    {
        var setup = await AddSetupAsync();
        await SetCourseAsync(setup.CourseId, "Beskrivning", new DateOnly(2025, 1, 10));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            (await db.Courses.SingleAsync(row => row.Id == setup.CourseId)).EndsOn = new DateOnly(2025, 6, 1);
            await db.SaveChangesAsync();
        }

        var teaching = await GetTeachingAsync(setup.TeacherId);

        var course = teaching.Courses.Single();
        Assert.Equal((new DateOnly(2025, 1, 10), new DateOnly(2025, 6, 1)), (course.StartsOn, course.EndsOn));
    }

    [Fact]
    public async Task NeedsPreparation_IsFalse_ForARunningCourseWithADescription()
    {
        var setup = await AddSetupAsync();
        await SetCourseAsync(setup.CourseId, "Beskrivning", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7));

        var teaching = await GetTeachingAsync(setup.TeacherId);

        Assert.False(teaching.Courses.Single().NeedsPreparation);
    }

    [Fact]
    public async Task NeedsPreparation_IsTrue_ForAnUpcomingCourseWithoutPublishedParts_UntilOneIsPublished()
    {
        var setup = await AddSetupAsync();
        await SetCourseAsync(setup.CourseId, "Beskrivning", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14));
        var week = await _client.CreateModuleAsync(setup.CourseId);

        var before = await GetTeachingAsync(setup.TeacherId);
        await _client.CreateTheoryLinkAsync("Del 1", new { courseId = setup.CourseId, moduleId = week.Id });
        var after = await GetTeachingAsync(setup.TeacherId);

        Assert.True(before.Courses.Single().NeedsPreparation);
        Assert.False(after.Courses.Single().NeedsPreparation);
    }

    [Fact]
    public async Task ToGrade_ListsUngradedSubmissionsInOwnCourses_OldestFirst()
    {
        var setup = await AddSetupAsync();
        var now = DateTimeOffset.UtcNow;
        var older = await _client.CreateAssignmentLinkAsync(
            "Äldre", new { courseId = setup.CourseId, dueAt = now.AddDays(2) });
        var newer = await _client.CreateAssignmentLinkAsync(
            "Nyare", new { courseId = setup.CourseId, dueAt = now.AddDays(1) });
        var olderSubmission = await SetSubmissionAsync(
            older, setup.StudentId, SubmissionStatus.Submitted, now.AddHours(-2));
        var newerSubmission = await SetSubmissionAsync(
            newer, setup.OtherStudentId, SubmissionStatus.Submitted, now.AddHours(-1));

        var teaching = await GetTeachingAsync(setup.TeacherId);

        Assert.Equal(
            [olderSubmission, newerSubmission],
            teaching.ToGrade.Select(item => item.SubmissionId));
        Assert.Equal(2, teaching.ToGradeTotal);
        var first = teaching.ToGrade[0];
        Assert.Equal(
            ("Äldre", setup.CourseId, "Kurs", setup.StudentId, "Test Testsson"),
            (first.Title, first.CourseId, first.CourseTitle, first.StudentId, first.StudentName));
    }

    [Fact]
    public async Task ToGrade_ExcludesGradedAndNeedsWork_AndOtherCourses()
    {
        var setup = await AddSetupAsync();
        var now = DateTimeOffset.UtcNow;
        var graded = await _client.CreateAssignmentLinkAsync("Bedömd", new { courseId = setup.CourseId });
        var needsWork = await _client.CreateAssignmentLinkAsync("Komplettering", new { courseId = setup.CourseId });
        var waiting = await _client.CreateAssignmentLinkAsync("Väntar", new { courseId = setup.CourseId });
        var otherCourse = await _client.CreateAssignmentLinkAsync(
            "Annan kurs", new { courseId = setup.OtherCourseId, dueAt = now.AddDays(2) });
        await SetSubmissionAsync(graded, setup.StudentId, SubmissionStatus.Graded, now.AddHours(-3));
        await SetSubmissionAsync(needsWork, setup.StudentId, SubmissionStatus.NeedsWork, now.AddHours(-2));
        await SetSubmissionAsync(waiting, setup.StudentId, SubmissionStatus.Submitted, now.AddHours(-1));
        await SetSubmissionAsync(
            otherCourse, setup.OtherGroupStudentId, SubmissionStatus.Submitted, now.AddHours(-1));
        await _client.CreateModuleAsync(setup.OtherCourseId, publishAt: now.AddDays(2));

        var teaching = await GetTeachingAsync(setup.TeacherId);

        var item = Assert.Single(teaching.ToGrade);
        Assert.Equal("Väntar", item.Title);
        Assert.Equal(1, teaching.ToGradeTotal);
        Assert.Empty(teaching.Deadlines);
        Assert.Empty(teaching.Scheduled);
        var course = Assert.Single(teaching.Courses);
        Assert.Equal(setup.CourseId, course.CourseId);
        Assert.Equal(1, course.ToGrade);
    }

    [Fact]
    public async Task Deadlines_ShowPublishedAssignmentsWithinTwoWeeks_WithCounts()
    {
        var setup = await AddSetupAsync();
        var now = DateTimeOffset.UtcNow;
        var soon = await _client.CreateAssignmentLinkAsync(
            "Snart", new { courseId = setup.CourseId, dueAt = now.AddDays(3) });
        await _client.CreateAssignmentLinkAsync(
            "Om en månad", new { courseId = setup.CourseId, dueAt = now.AddDays(30) });
        var draft = await _client.CreateContentAsync(new
        {
            type = "assignment",
            title = "Utkast",
            bodyMd = "",
            status = "draft",
            submissionModes = new[] { "text" }
        });
        await _client.CreateLinkAsync(draft.Id, new { courseId = setup.CourseId, dueAt = now.AddDays(4) });
        await SetSubmissionAsync(soon, setup.StudentId, SubmissionStatus.Submitted, now.AddHours(-1));

        var teaching = await GetTeachingAsync(setup.TeacherId);

        var deadline = Assert.Single(teaching.Deadlines);
        Assert.Equal(
            ("Snart", setup.CourseId, "Kurs"),
            (deadline.Title, deadline.CourseId, deadline.CourseTitle));
        Assert.Equal((1, 2), (deadline.Submitted, deadline.Students));
        var course = Assert.Single(teaching.Courses);
        Assert.Equal((2, 1), (course.Students, course.ToGrade));
    }

    [Fact]
    public async Task Scheduled_ShowsWeeksAndPartsWithinSevenDays()
    {
        var setup = await AddSetupAsync();
        var now = DateTimeOffset.UtcNow;
        var soonWeek = await _client.CreateModuleAsync(setup.CourseId, publishAt: now.AddDays(2));
        await _client.CreateModuleAsync(setup.CourseId, publishAt: now.AddDays(10));
        var part = await _client.CreateTheoryLinkAsync(
            "Ny del", new { courseId = setup.CourseId, moduleId = soonWeek.Id, publishAt = now.AddDays(3) });

        var teaching = await GetTeachingAsync(setup.TeacherId);

        Assert.Equal(
            [("Week", soonWeek.Id), ("Part", part)],
            teaching.Scheduled.Select(item => (item.Kind, item.ModuleId ?? item.LinkId)));
        var week = teaching.Scheduled[0];
        Assert.Null(week.LinkId);
        Assert.Equal((soonWeek.Id, "Kurs"), (week.ModuleId, week.CourseTitle));
        var scheduledPart = teaching.Scheduled[1];
        Assert.Equal((part, (Guid?)null), (scheduledPart.LinkId, scheduledPart.ModuleId));
    }

    [Fact]
    public async Task Student_GetsEmptyLists()
    {
        var setup = await AddSetupAsync();
        var now = DateTimeOffset.UtcNow;
        var link = await _client.CreateAssignmentLinkAsync(
            "Uppgift", new { courseId = setup.CourseId, dueAt = now.AddDays(2) });
        await SetSubmissionAsync(link, setup.StudentId, SubmissionStatus.Submitted, now.AddHours(-1));
        await _client.CreateModuleAsync(setup.CourseId, publishAt: now.AddDays(2));

        var teaching = await GetTeachingAsync(setup.StudentId);

        Assert.Empty(teaching.ToGrade);
        Assert.Equal(0, teaching.ToGradeTotal);
        Assert.Empty(teaching.Deadlines);
        Assert.Empty(teaching.Scheduled);
        Assert.Empty(teaching.Courses);
    }

    private async Task<Setup> AddSetupAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var otherGroup = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
        var otherCourse = await TestData.AddCourseAsync(db, school.Id, otherGroup.Id);
        var student = await TestData.AddMemberAsync(db, group);
        var otherStudent = await TestData.AddMemberAsync(db, group);
        var otherGroupStudent = await TestData.AddMemberAsync(db, otherGroup);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        await TestData.AddCourseTeacherAsync(db, course, teacher);
        return new Setup(
            group.Id,
            otherGroup.Id,
            course.Id,
            otherCourse.Id,
            student.Id,
            otherStudent.Id,
            otherGroupStudent.Id,
            teacher.Id);
    }

    private async Task SetCourseAsync(Guid courseId, string description, DateOnly startsOn)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var course = await db.Courses.SingleAsync(row => row.Id == courseId);
        course.Description = description;
        course.StartsOn = startsOn;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SetSubmissionAsync(
        Guid linkId, Guid studentId, SubmissionStatus status, DateTimeOffset submittedAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submission = await db.Submissions.SingleAsync(row =>
            row.ContentLinkId == linkId && row.StudentId == studentId);
        submission.Status = status;
        submission.SubmittedAt = submittedAt;
        await db.SaveChangesAsync();
        return submission.Id;
    }

    private async Task<TeachingResponse> GetTeachingAsync(Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me/teaching");
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeachingResponse>())!;
    }

}

public class TeachingAuthenticationTests(JwtTestWebApplicationFactory factory)
    : IClassFixture<JwtTestWebApplicationFactory>
{
    [Fact]
    public async Task TeachingWithoutToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/teaching");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
