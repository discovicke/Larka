using System.Net;
using System.Net.Http.Json;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class NotificationEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record IdResponse(Guid Id);

    private record CreatedResponse(IdResponse Content, IdResponse Link);

    private record SubmissionResponse(Guid Id);

    private record NotificationResponse(
        Guid Id,
        string Kind,
        Guid? ContentId,
        Guid? SubmissionId,
        Guid? LinkId,
        string? Title,
        Guid? CourseId,
        string? CourseTitle,
        string? GroupName,
        string? Level,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReadAt);

    private record ListResponse(List<NotificationResponse> Items, int UnreadCount);

    private record CountResponse(int Count);

    private record Setup(Guid GroupId, Guid CourseId, Guid StudentId, Guid OtherStudentId, Guid TeacherId);

    [Fact]
    public async Task PublishingNewsToAGroup_NotifiesMembersExceptTheAuthor()
    {
        var setup = await AddSetupAsync();

        var created = await CreateAsync(News("Studiebesök"), new { groupId = setup.GroupId });

        var student = await ListForAsync(setup.StudentId);
        var item = Assert.Single(student.Items);
        Assert.Equal(("NewsPublished", "Studiebesök", created.Content.Id), (item.Kind, item.Title, item.ContentId));
        Assert.Equal(created.Link.Id, item.LinkId);
        Assert.Null(item.ReadAt);
        Assert.Single((await ListForAsync(setup.OtherStudentId)).Items);
        Assert.Single((await ListForAsync(setup.TeacherId)).Items);
        Assert.Empty((await ListForAsync(PlaceholderIds.UserId)).Items);
    }

    [Fact]
    public async Task NotificationTellsWhichGroupAndCourseItHappenedIn()
    {
        var setup = await AddSetupAsync();

        await CreateAsync(News("Gruppnyhet"), new { groupId = setup.GroupId });
        await CreateAsync(News("Kursnyhet"), new { courseId = setup.CourseId });

        var items = (await ListForAsync(setup.StudentId)).Items;
        var groupNews = items.Single(item => item.Title == "Gruppnyhet");
        Assert.Null(groupNews.CourseTitle);
        Assert.NotNull(groupNews.GroupName);
        var courseNews = items.Single(item => item.Title == "Kursnyhet");
        Assert.Equal(("Kurs", setup.CourseId), (courseNews.CourseTitle, courseNews.CourseId));
    }

    [Fact]
    public async Task PublishingToACourse_NotifiesEveryGroupReadingIt_AndNobodyElse()
    {
        var setup = await AddSetupAsync();
        var outsider = await AddUserOutsideAsync();

        await CreateAsync(News("Till kursen"), new { courseId = setup.CourseId });

        Assert.Single((await ListForAsync(setup.StudentId)).Items);
        Assert.Single((await ListForAsync(setup.OtherStudentId)).Items);
        Assert.Empty((await ListForAsync(outsider)).Items);
    }

    [Fact]
    public async Task Assignments_AreNotified_ButTheoryIsNot()
    {
        var setup = await AddSetupAsync();

        await CreateAsync(
            new { type = "assignment", title = "Labb", bodyMd = "", submissionModes = new[] { "text" } },
            new { groupId = setup.GroupId });
        await CreateAsync(new { type = "theory", title = "Läsning", bodyMd = "" }, new { groupId = setup.GroupId });

        var item = Assert.Single((await ListForAsync(setup.StudentId)).Items);
        Assert.Equal(("AssignmentPublished", "Labb"), (item.Kind, item.Title));
    }

    [Fact]
    public async Task UserWhoMutedTheKind_GetsNoNotification_WhileOthersDo()
    {
        var setup = await AddSetupAsync();
        await UpdateDbAsync(db => db.Users
            .Where(user => user.Id == setup.StudentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                user => user.MutedNotificationKinds, [NotificationKind.NewsPublished])));

        await CreateAsync(News("Tyst"), new { groupId = setup.GroupId });

        Assert.Empty((await ListForAsync(setup.StudentId)).Items);
        Assert.Single((await ListForAsync(setup.OtherStudentId)).Items);
    }

    [Fact]
    public async Task Draft_NotifiesOnlyOnceItIsPublished_AndNeverTwice()
    {
        var setup = await AddSetupAsync();
        var created = await CreateAsync(
            new { type = "news", title = "Utkast", bodyMd = "", status = "draft" },
            new { groupId = setup.GroupId });
        Assert.Empty((await ListForAsync(setup.StudentId)).Items);

        await SetStatusAsync(created.Content.Id, "published");
        await SetStatusAsync(created.Content.Id, "draft");
        await SetStatusAsync(created.Content.Id, "published");

        Assert.Single((await ListForAsync(setup.StudentId)).Items);
    }

    [Fact]
    public async Task ScheduledPublication_NotifiesWhenPublishAtPasses_AndOnlyOnce()
    {
        var setup = await AddSetupAsync();
        var created = await CreateAsync(
            News("Senare"), new { groupId = setup.GroupId, publishAt = DateTimeOffset.UtcNow.AddHours(2) });
        Assert.Empty((await ListForAsync(setup.StudentId)).Items);
        Assert.Equal(0, await RunJobAsync());

        await UpdateDbAsync(db => db.ContentLinks
            .Where(link => link.Id == created.Link.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                link => link.PublishAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        Assert.Equal(3, await RunJobAsync());
        Assert.Equal(0, await RunJobAsync());
        Assert.Single((await ListForAsync(setup.StudentId)).Items);
        Assert.Single((await ListForAsync(setup.TeacherId)).Items);
    }

    [Fact]
    public async Task Publication_InAClosedWeek_WaitsForTheWeek()
    {
        var setup = await AddSetupAsync();
        var module = await AddModuleAsync(setup.CourseId, DateTimeOffset.UtcNow.AddDays(1));

        var created = await CreateAsync(
            new { type = "assignment", title = "Vecka 2", bodyMd = "", submissionModes = new[] { "text" } },
            new { courseId = setup.CourseId, moduleId = module });
        Assert.Empty((await ListForAsync(setup.StudentId)).Items);

        await UpdateDbAsync(db => db.Modules
            .Where(candidate => candidate.Id == module)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                candidate => candidate.PublishAt, DateTimeOffset.UtcNow.AddMinutes(-1))));
        await UpdateDbAsync(db => db.ContentLinks
            .Where(link => link.Id == created.Link.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                link => link.PublishAt, DateTimeOffset.UtcNow.AddDays(-2))));

        await RunJobAsync();
        Assert.Single((await ListForAsync(setup.StudentId)).Items);
    }

    [Fact]
    public async Task Grading_NotifiesTheStudent_AndNeedsWorkIsItsOwnKind()
    {
        var setup = await AddSetupAsync();
        var created = await CreateAsync(
            new
            {
                type = "assignment",
                title = "Betygsatt",
                bodyMd = "",
                submissionModes = new[] { "text" },
                gradeScale = "passFailDistinction"
            },
            new { groupId = setup.GroupId });
        var submission = await SubmissionOfAsync(created.Link.Id, setup.StudentId);

        await _client.PostAsJsonAsync($"/api/submissions/{submission}/grade",
            new { feedback = "Komplettera", needsWork = true });
        await _client.PostAsJsonAsync($"/api/submissions/{submission}/grade",
            new { feedback = "Bra", level = "distinction" });

        var items = (await ListForAsync(setup.StudentId)).Items;
        Assert.Equal(["SubmissionGraded", "SubmissionNeedsWork"],
            items.Where(item => item.SubmissionId != null).OrderByDescending(item => item.CreatedAt)
                .Select(item => item.Kind));
        var graded = items.First(item => item.Kind == "SubmissionGraded");
        Assert.Equal(("Betygsatt", "Distinction", submission, created.Link.Id),
            (graded.Title, graded.Level, graded.SubmissionId, graded.LinkId));
        Assert.DoesNotContain((await ListForAsync(setup.OtherStudentId)).Items, item => item.SubmissionId != null);
    }

    [Fact]
    public async Task GradeNotification_InACourseWeek_CarriesCourseAndLink()
    {
        var setup = await AddSetupAsync();
        var module = await AddModuleAsync(setup.CourseId, DateTimeOffset.UtcNow.AddDays(-1));
        var created = await CreateAsync(
            new
            {
                type = "assignment",
                title = "Kursuppgift",
                bodyMd = "",
                submissionModes = new[] { "text" },
                gradeScale = "passFail"
            },
            new { courseId = setup.CourseId, moduleId = module });
        var submission = await SubmissionOfAsync(created.Link.Id, setup.StudentId);

        await _client.PostAsJsonAsync($"/api/submissions/{submission}/grade",
            new { feedback = "Bra", level = "pass" });

        var graded = (await ListForAsync(setup.StudentId)).Items.First(item => item.Kind == "SubmissionGraded");
        Assert.Equal((setup.CourseId, created.Link.Id), (graded.CourseId, graded.LinkId));
    }

    [Fact]
    public async Task DeadlineReminder_GoesToStudentsWhoHaveNotSubmitted_AndIsIdempotent()
    {
        var setup = await AddSetupAsync();
        var created = await CreateAsync(
            new { type = "assignment", title = "Snart", bodyMd = "", submissionModes = new[] { "text" } },
            new { groupId = setup.GroupId, dueAt = DateTimeOffset.UtcNow.AddHours(12) });
        var submitted = await SubmissionOfAsync(created.Link.Id, setup.OtherStudentId);
        await UpdateDbAsync(db => db.Submissions
            .Where(submission => submission.Id == submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(submission => submission.Status, SubmissionStatus.Submitted)));

        Assert.Equal(1, await RunJobAsync());
        Assert.Equal(0, await RunJobAsync());
        Assert.Equal(0, await RunJobAsync());

        var reminder = (await ListForAsync(setup.StudentId)).Items.Single(item => item.Kind == "DeadlineSoon");
        Assert.Equal(("Snart", created.Content.Id), (reminder.Title, reminder.ContentId));
        Assert.DoesNotContain((await ListForAsync(setup.OtherStudentId)).Items, item => item.Kind == "DeadlineSoon");
        Assert.DoesNotContain((await ListForAsync(setup.TeacherId)).Items, item => item.Kind == "DeadlineSoon");
    }

    [Fact]
    public async Task DeadlineReminder_IsNotSentFarAheadOrAfterTheDeadline()
    {
        var setup = await AddSetupAsync();
        await CreateAsync(
            new { type = "assignment", title = "Långt bort", bodyMd = "", submissionModes = new[] { "text" } },
            new { groupId = setup.GroupId, dueAt = DateTimeOffset.UtcNow.AddDays(3) });
        await CreateAsync(
            new { type = "assignment", title = "Passerad", bodyMd = "", submissionModes = new[] { "text" } },
            new { groupId = setup.GroupId, dueAt = DateTimeOffset.UtcNow.AddHours(-1) });

        await RunJobAsync();

        Assert.DoesNotContain((await ListForAsync(setup.StudentId)).Items, item => item.Kind == "DeadlineSoon");
    }

    [Fact]
    public async Task MarkRead_SetsReadAt_AndLowersTheUnreadCount()
    {
        var setup = await AddSetupAsync();
        await CreateAsync(News("Ett"), new { groupId = setup.GroupId });
        await CreateAsync(News("Två"), new { groupId = setup.GroupId });
        var list = await ListForAsync(setup.StudentId);
        Assert.Equal(2, list.UnreadCount);

        var response = await PostAsAsync(setup.StudentId, $"/api/notifications/{list.Items[0].Id}/read");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await ListForAsync(setup.StudentId);
        Assert.Equal(1, after.UnreadCount);
        Assert.NotNull(after.Items.Single(item => item.Id == list.Items[0].Id).ReadAt);
        Assert.Equal(1, (await CountForAsync(setup.StudentId)).Count);
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyTheUsersOwnNotifications()
    {
        var setup = await AddSetupAsync();
        await CreateAsync(News("Ett"), new { groupId = setup.GroupId });
        await CreateAsync(News("Två"), new { groupId = setup.GroupId });

        var response = await PostAsAsync(setup.StudentId, "/api/notifications/read-all");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var mine = await ListForAsync(setup.StudentId);
        Assert.Equal(0, mine.UnreadCount);
        Assert.All(mine.Items, item => Assert.NotNull(item.ReadAt));
        Assert.Equal(2, (await ListForAsync(setup.OtherStudentId)).UnreadCount);
    }

    [Fact]
    public async Task MarkRead_OnSomeoneElsesNotification_ReturnsNotFound_AndLeavesItUnread()
    {
        var setup = await AddSetupAsync();
        await CreateAsync(News("Ett"), new { groupId = setup.GroupId });
        var theirs = (await ListForAsync(setup.StudentId)).Items.Single();

        var response = await PostAsAsync(setup.OtherStudentId, $"/api/notifications/{theirs.Id}/read");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null((await ListForAsync(setup.StudentId)).Items.Single().ReadAt);
        Assert.Equal(HttpStatusCode.NotFound,
            (await PostAsAsync(setup.StudentId, $"/api/notifications/{Guid.NewGuid()}/read")).StatusCode);
    }

    [Fact]
    public async Task List_IsNewestFirst_AndRespectsTheLimit()
    {
        var setup = await AddSetupAsync();
        await CreateAsync(News("Äldst"), new { groupId = setup.GroupId });
        await CreateAsync(News("Mitten"), new { groupId = setup.GroupId });
        await CreateAsync(News("Nyast"), new { groupId = setup.GroupId });

        var all = await ListForAsync(setup.StudentId);
        var limited = await GetAsAsync<ListResponse>(setup.StudentId, "/api/notifications?limit=2");

        Assert.Equal(["Nyast", "Mitten", "Äldst"], all.Items.Select(item => item.Title));
        Assert.Equal(["Nyast", "Mitten"], limited.Items.Select(item => item.Title));
        Assert.Equal(3, limited.UnreadCount);
    }

    [Fact]
    public async Task Notification_SurvivesItsContentBeingDeleted_WithoutATitle()
    {
        var setup = await AddSetupAsync();
        var created = await CreateAsync(News("Borta"), new { groupId = setup.GroupId });
        await _client.DeleteAsync($"/api/content/{created.Content.Id}/links/{created.Link.Id}");

        var item = Assert.Single((await ListForAsync(setup.StudentId)).Items);

        Assert.Null(item.Title);
        Assert.Null(item.ContentId);
    }

    private static object News(string title) => new { type = "news", title, bodyMd = "" };

    private async Task<CreatedResponse> CreateAsync(object content, object link)
    {
        var response = await _client.PostAsJsonAsync("/api/content/with-link", new { content, link });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>())!;
    }

    private async Task SetStatusAsync(Guid contentId, string status)
    {
        var response = await _client.PutAsJsonAsync($"/api/content/{contentId}/status", new { status });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Setup> AddSetupAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db);
        var course = await TestData.AddCourseAsync(db, group.SchoolId, group.Id);
        var student = await TestData.AddMemberAsync(db, group);
        var otherStudent = await TestData.AddMemberAsync(db, group);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);

        // Författaren (placeholder-admin) är själv medlem i gruppen och ska ändå inte få notisen.
        db.Enrollments.Add(new Enrollment
        {
            UserId = PlaceholderIds.UserId,
            GroupId = group.Id,
            Role = GroupRole.Teacher
        });
        await db.SaveChangesAsync();
        return new Setup(group.Id, course.Id, student.Id, otherStudent.Id, teacher.Id);
    }

    private async Task<Guid> AddUserOutsideAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db);
        return (await TestData.AddMemberAsync(db, group)).Id;
    }

    private async Task<Guid> AddModuleAsync(Guid courseId, DateTimeOffset publishAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var module = new Module { CourseId = courseId, Title = "Vecka 2", Position = 1, PublishAt = publishAt };
        db.Modules.Add(module);
        await db.SaveChangesAsync();
        return module.Id;
    }

    private async Task<Guid> SubmissionOfAsync(Guid linkId, Guid studentId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submission = await db.Submissions.SingleAsync(candidate =>
            candidate.ContentLinkId == linkId && candidate.StudentId == studentId);
        if (submission.Status == SubmissionStatus.NotStarted && submission.Grade is null)
        {
            submission.Status = SubmissionStatus.Submitted;
            submission.SubmittedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        return submission.Id;
    }

    private async Task UpdateDbAsync(Func<AppDb, Task> change)
    {
        using var scope = factory.Services.CreateScope();
        await change(scope.ServiceProvider.GetRequiredService<AppDb>());
    }

    private async Task<int> RunJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationService>()
            .CreateScheduledAsync(CancellationToken.None);
    }

    private Task<ListResponse> ListForAsync(Guid userId) => GetAsAsync<ListResponse>(userId, "/api/notifications");

    private Task<CountResponse> CountForAsync(Guid userId) =>
        GetAsAsync<CountResponse>(userId, "/api/notifications/unread-count");

    private async Task<T> GetAsAsync<T>(Guid userId, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> PostAsAsync(Guid userId, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }
}
