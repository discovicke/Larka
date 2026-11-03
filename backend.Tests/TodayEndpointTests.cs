using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class TodayEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Setup(Guid GroupId, Guid OtherGroupId, Guid CourseId, Guid OtherCourseId, Guid StudentId,
        Guid OtherStudentId, Guid TeacherId);

    private record TodayResponse(
        List<TodoResponse> Todo,
        List<SubmittedResponse> Submitted,
        List<FeedbackResponse> Feedback,
        List<NewsResponse> News,
        List<CourseResponse> Courses);

    private record TodoResponse(Guid LinkId, string Title, Guid? CourseId, string? CourseTitle,
        DateTimeOffset DueAt, string Status);

    private record SubmittedResponse(Guid LinkId, string Title, string Status, string? Grade);

    private record FeedbackResponse(Guid LinkId, string Title, string? Grade, string Feedback);

    private record NewsResponse(string Title, string? Source);

    private record CourseResponse(Guid CourseId, string Code, int TotalParts, int CompletedParts,
        DateOnly? StartsOn, DateOnly? EndsOn);

    private record ContentResponse(Guid Id);

    private record LinkResponse(Guid Id);

    private record ModuleResponse(Guid Id);

    [Fact]
    public async Task Todo_ListsOwnUpcomingAssignments_ByDueDate()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var soon = DateTimeOffset.UtcNow.AddDays(1);
        var later = DateTimeOffset.UtcNow.AddDays(9);
        await CreateAssignmentLinkAsync("Senare", new { courseId = setup.CourseId, moduleId = week.Id, dueAt = later });
        await CreateAssignmentLinkAsync("Snart", new { groupId = setup.GroupId, dueAt = soon });
        await CreateAssignmentLinkAsync("Förfallen",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await CreateAssignmentLinkAsync("Utan deadline", new { courseId = setup.CourseId, moduleId = week.Id });

        var today = await GetTodayAsync(setup.StudentId);

        Assert.Equal(["Snart", "Senare"], today.Todo.Select(item => item.Title));
        var first = today.Todo.Single(item => item.Title == "Senare");
        Assert.Equal((setup.CourseId, "Kurs", "NotStarted"), (first.CourseId, first.CourseTitle, first.Status));
    }

    [Fact]
    public async Task Todo_ExcludesUnpublishedAndOtherGroupsContent()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var futureWeek = await CreateModuleAsync(setup.CourseId, DateTimeOffset.UtcNow.AddDays(3));
        var due = DateTimeOffset.UtcNow.AddDays(2);
        await CreateAssignmentLinkAsync("Synlig", new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        await CreateAssignmentLinkAsync("Publiceras senare", new
        {
            courseId = setup.CourseId,
            moduleId = week.Id,
            publishAt = DateTimeOffset.UtcNow.AddDays(1),
            dueAt = due
        });
        await CreateAssignmentLinkAsync("Framtida vecka",
            new { courseId = setup.CourseId, moduleId = futureWeek.Id, dueAt = due });
        var draft = await CreateContentAsync(
            new { type = "assignment", title = "Utkast", bodyMd = "", status = "draft" });
        await CreateLinkAsync(draft.Id, new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        await CreateAssignmentLinkAsync("Annan grupp",
            new { groupId = setup.OtherGroupId, courseId = setup.OtherCourseId, dueAt = due });

        var today = await GetTodayAsync(setup.StudentId);

        Assert.Equal(["Synlig"], today.Todo.Select(item => item.Title));
    }

    [Fact]
    public async Task Todo_ExcludesSubmittedItems_ButKeepsOnesNeedingWork()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var due = DateTimeOffset.UtcNow.AddDays(2);
        var submitted = await CreateAssignmentLinkAsync("Inlämnad",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        var needsWork = await CreateAssignmentLinkAsync("Komplettera",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        await CreateAssignmentLinkAsync(
            "Ej inlämnad",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        await AddSubmissionAsync(submitted, setup.StudentId, SubmissionStatus.Submitted);
        await AddSubmissionAsync(needsWork, setup.StudentId, SubmissionStatus.NeedsWork, feedback: "Lägg till tester");
        // En annan elevs inlämning ska inte påverka listan.
        await AddSubmissionAsync(
            await CreateAssignmentLinkAsync("Någon annans",
                new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due }),
            setup.OtherStudentId, SubmissionStatus.Submitted);

        var today = await GetTodayAsync(setup.StudentId);

        Assert.Equal(
            new[] { "Komplettera", "Någon annans", "Ej inlämnad" }.Order(),
            today.Todo.Select(item => item.Title).Order());
        Assert.Equal("NeedsWork", today.Todo.Single(item => item.Title == "Komplettera").Status);
        Assert.DoesNotContain(today.Todo, item => item.Title == "Inlämnad");
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public async Task Todo_ShowsAPersonalDeadline_AfterTheClassDeadline_AndSortsByIt(SubmissionStatus status)
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var personal = DateTimeOffset.UtcNow.AddDays(3);
        var late = await CreateAssignmentLinkAsync("Egen deadline",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = DateTimeOffset.UtcNow.AddDays(-4) });
        await CreateAssignmentLinkAsync("Om en vecka",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = DateTimeOffset.UtcNow.AddDays(7) });
        await CreateAssignmentLinkAsync("Om två dagar",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = DateTimeOffset.UtcNow.AddDays(2) });
        await AddSubmissionAsync(late, setup.StudentId, status);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            (await db.Submissions.SingleAsync(row => row.ContentLinkId == late && row.StudentId == setup.StudentId))
                .PersonalDueAt = personal;
            await db.SaveChangesAsync();
        }

        var today = await GetTodayAsync(setup.StudentId);

        Assert.Equal(["Om två dagar", "Egen deadline", "Om en vecka"], today.Todo.Select(item => item.Title));
        var shown = today.Todo.Single(item => item.Title == "Egen deadline");
        Assert.Equal(personal.UtcDateTime, shown.DueAt.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Todo_KeepsANeedsWorkAssignment_WhileAKomplettringDeadlineRemains_AndDropsItAfterTheLast()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var passed = DateTimeOffset.UtcNow.AddDays(-5);
        var open = await CreateAssignmentLinkAsync("Komplettera före kompletteringsdeadline",
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                dueAt = passed,
                resubmitDueAt = new[] { DateTimeOffset.UtcNow.AddDays(3) }
            });
        var closed = await CreateAssignmentLinkAsync("Komplettering missad",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = passed });
        await CreateAssignmentLinkAsync("Missad",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = passed });
        var graded = await CreateAssignmentLinkAsync("Bedömd",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = passed });
        await AddSubmissionAsync(open, setup.StudentId, SubmissionStatus.NeedsWork, feedback: "Lägg till tester");
        await AddSubmissionAsync(closed, setup.StudentId, SubmissionStatus.NeedsWork, feedback: "Lägg till tester");
        await AddSubmissionAsync(graded, setup.StudentId, SubmissionStatus.Graded);

        var today = await GetTodayAsync(setup.StudentId);

        // Kompletteringen står kvar så länge en kompletteringsdeadline återstår. Efter sista deadline har eleven
        // automatiskt IG och inget kvar att göra.
        var item = Assert.Single(today.Todo);
        Assert.Equal("Komplettera före kompletteringsdeadline", item.Title);
        Assert.Equal("NeedsWork", item.Status);
    }

    [Fact]
    public async Task Submitted_AndFeedback_AreTheStudentsOwn()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var due = DateTimeOffset.UtcNow.AddDays(2);
        var graded = await CreateAssignmentLinkAsync("Betygsatt",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        var handedIn = await CreateAssignmentLinkAsync("Väntar på betyg",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        var notStarted = await CreateAssignmentLinkAsync("Ej påbörjad",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = due });
        await AddSubmissionAsync(graded, setup.StudentId, SubmissionStatus.Graded, GradeLevel.Pass, "Bra jobbat");
        await AddSubmissionAsync(handedIn, setup.StudentId, SubmissionStatus.Submitted);
        await AddSubmissionAsync(notStarted, setup.StudentId, SubmissionStatus.NotStarted);
        await AddSubmissionAsync(graded, setup.OtherStudentId, SubmissionStatus.Graded, GradeLevel.Fail, "Annan elev");

        var today = await GetTodayAsync(setup.StudentId);

        Assert.Equal(
            new[] { "Betygsatt", "Väntar på betyg" }.Order(),
            today.Submitted.Select(item => item.Title).Order());
        Assert.Equal("Pass", today.Submitted.Single(item => item.Title == "Betygsatt").Grade);
        var feedback = Assert.Single(today.Feedback);
        Assert.Equal(("Betygsatt", "Pass", "Bra jobbat"), (feedback.Title, feedback.Grade, feedback.Feedback));
        Assert.DoesNotContain(today.Feedback, item => item.Feedback == "Annan elev");
    }

    [Fact]
    public async Task News_ComesFromOwnGroupsAndCourses_AndIsPublished()
    {
        var setup = await AddSetupAsync();
        await CreateNewsAsync("Till gruppen", new { groupId = setup.GroupId });
        await CreateNewsAsync("Till kursen", new { courseId = setup.CourseId });
        await CreateNewsAsync("Annan grupp", new { groupId = setup.OtherGroupId });
        await CreateNewsAsync("Senare", new { groupId = setup.GroupId, publishAt = DateTimeOffset.UtcNow.AddDays(1) });

        var today = await GetTodayAsync(setup.StudentId);

        Assert.Equal(new[] { "Till gruppen", "Till kursen" }.Order(), today.News.Select(item => item.Title).Order());
        Assert.Equal("Kurs", today.News.Single(item => item.Title == "Till kursen").Source);
    }

    [Fact]
    public async Task Courses_CountCompletedParts_OnlyForOwnCourses()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var done = await CreateAssignmentLinkAsync("Klar", new { courseId = setup.CourseId, moduleId = week.Id });
        await CreateAssignmentLinkAsync("Inte klar", new { courseId = setup.CourseId, moduleId = week.Id });
        await CreateNewsAsync("En nyhet", new { courseId = setup.CourseId, moduleId = week.Id });
        await AddSubmissionAsync(done, setup.StudentId, SubmissionStatus.Submitted);

        var today = await GetTodayAsync(setup.StudentId);

        var course = Assert.Single(today.Courses);
        Assert.Equal((setup.CourseId, 2, 1), (course.CourseId, course.TotalParts, course.CompletedParts));
    }

    [Fact]
    public async Task Courses_DoNotCountAPartThatNeedsWork_AsCompleted()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var link = new { courseId = setup.CourseId, moduleId = week.Id };
        var submitted = await CreateAssignmentLinkAsync("Inlämnad", link);
        var needsWork = await CreateAssignmentLinkAsync("Komplettera", link);
        await AddSubmissionAsync(submitted, setup.StudentId, SubmissionStatus.Submitted);
        await AddSubmissionAsync(needsWork, setup.StudentId, SubmissionStatus.NeedsWork);

        var today = await GetTodayAsync(setup.StudentId);

        var course = Assert.Single(today.Courses);
        Assert.Equal((2, 1), (course.TotalParts, course.CompletedParts));
    }

    [Fact]
    public async Task Teacher_GetsNoStudentLists()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        await CreateAssignmentLinkAsync("Uppgift",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = DateTimeOffset.UtcNow.AddDays(2) });

        var today = await GetTodayAsync(setup.TeacherId);

        Assert.Empty(today.Todo);
        Assert.Empty(today.Courses);
    }

    [Fact]
    public async Task Courses_CarryTheCourseDates()
    {
        var setup = await AddSetupAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var course = await db.Courses.SingleAsync(row => row.Id == setup.CourseId);
            course.StartsOn = new DateOnly(2025, 1, 10);
            course.EndsOn = new DateOnly(2025, 6, 1);
            await db.SaveChangesAsync();
        }

        var today = await GetTodayAsync(setup.StudentId);

        var result = Assert.Single(today.Courses);
        Assert.Equal((new DateOnly(2025, 1, 10), new DateOnly(2025, 6, 1)), (result.StartsOn, result.EndsOn));
    }

    private async Task<Setup> AddSetupAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id, placeholderTeaches: true);
        var otherGroup = await TestData.AddGroupAsync(db, school.Id, placeholderTeaches: true);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
        var otherCourse = await TestData.AddCourseAsync(db, school.Id, otherGroup.Id);
        var student = await TestData.AddMemberAsync(db, group);
        var otherStudent = await TestData.AddMemberAsync(db, group);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        await TestData.AddCourseTeacherAsync(db, course, teacher);
        return new Setup(group.Id, otherGroup.Id, course.Id, otherCourse.Id, student.Id, otherStudent.Id, teacher.Id);
    }

    private async Task AddSubmissionAsync(
        Guid linkId, Guid studentId, SubmissionStatus status, GradeLevel? level = null, string feedback = "")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Utdelningen skapar en rad per elev i gruppen. Ersätt den med den här.
        var existing = db.Submissions.Where(submission =>
            submission.ContentLinkId == linkId && submission.StudentId == studentId);
        db.Submissions.RemoveRange(existing);
        var submission = new Submission
        {
            ContentLinkId = linkId,
            StudentId = studentId,
            Status = status,
            SubmittedAt = status == SubmissionStatus.NotStarted
                ? null
                : DateTimeOffset.UtcNow.AddHours(-1)
        };
        db.Submissions.Add(submission);
        if (status is SubmissionStatus.Graded or SubmissionStatus.NeedsWork)
        {
            db.Grades.Add(new Grade
            {
                SubmissionId = submission.Id,
                GradedById = (await TestData.AddUserAsync(db, role: UserRole.Teacher)).Id,
                Level = level,
                Feedback = feedback
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<TodayResponse> GetTodayAsync(Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me/today");
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodayResponse>())!;
    }

    private async Task<ModuleResponse> CreateModuleAsync(Guid courseId, DateTimeOffset? publishAt = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/courses/{courseId}/modules", new { title = "Vecka 1", publishAt });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ModuleResponse>())!;
    }

    private async Task<Guid> CreateAssignmentLinkAsync(string title, object link)
    {
        var content = await CreateContentAsync(
            new { type = "assignment", title, bodyMd = "", submissionModes = new[] { "text" } });
        return await CreateLinkAsync(content.Id, link);
    }

    private async Task CreateNewsAsync(string title, object link)
    {
        var content = await CreateContentAsync(new { type = "news", title, bodyMd = "Text" });
        await CreateLinkAsync(content.Id, link);
    }

    private async Task<ContentResponse> CreateContentAsync(object content)
    {
        var response = await _client.PostAsJsonAsync("/api/content", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }

    private async Task<Guid> CreateLinkAsync(Guid contentId, object link)
    {
        var response = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", link);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LinkResponse>())!.Id;
    }
}

public class TodayAuthenticationTests(JwtTestWebApplicationFactory factory)
    : IClassFixture<JwtTestWebApplicationFactory>
{
    [Fact]
    public async Task TodayWithoutToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/today");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
