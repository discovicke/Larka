using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class CalendarEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Setup(
        Guid GroupId, Guid OtherGroupId, Guid CourseId, Guid OtherCourseId, Guid StudentId, Guid TeacherId);

    private record ItemResponse(
        Guid LinkId,
        Guid ContentId,
        string Title,
        string ContentType,
        Guid? CourseId,
        string? CourseTitle,
        Guid? GroupId,
        string Kind,
        DateTimeOffset At);

    private record ContentResponse(Guid Id);

    private record LinkResponse(Guid Id);

    private record ModuleResponse(Guid Id);

    [Fact]
    public async Task Student_SeesDeadlinesOfOwnGroupsAndCoursesLinksInRange_Only()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        var courseDeadline = from.AddDays(1).AddHours(12);
        var groupDeadline = from.AddDays(2).AddHours(8);
        await CreateAssignmentLinkAsync(
            "Kursuppgift",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = courseDeadline });
        await CreateAssignmentLinkAsync(
            "Gruppuppgift",
            new { groupId = setup.GroupId, dueAt = groupDeadline });
        await CreateAssignmentLinkAsync(
            "Annan grupp",
            new
            {
                groupId = setup.OtherGroupId,
                courseId = setup.OtherCourseId,
                dueAt = from.AddDays(2)
            });
        await CreateAssignmentLinkAsync(
            "Utanför",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = to.AddDays(2) });

        var items = await GetCalendarAsync(setup.StudentId, from, to);

        Assert.Equal(["Kursuppgift", "Gruppuppgift"], items.Select(item => item.Title));
        Assert.All(items, item => Assert.Equal("Deadline", item.Kind));
        var courseItem = items.Single(item => item.Title == "Kursuppgift");
        Assert.Equal((setup.CourseId, "Kurs", "Assignment"), (courseItem.CourseId, courseItem.CourseTitle,
            courseItem.ContentType));
        Assert.Equal(courseDeadline, courseItem.At);
        var groupItem = items.Single(item => item.Title == "Gruppuppgift");
        Assert.Equal((setup.GroupId, (Guid?)null), (groupItem.GroupId, groupItem.CourseId));
        Assert.Equal(groupDeadline, groupItem.At);
    }

    [Fact]
    public async Task Student_DoesNotSeeAnotherGroupsLinkInASharedCourse()
    {
        var setup = await AddSetupAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.GroupCourses.Add(new Backend.Domain.Entities.GroupCourse
            {
                GroupId = setup.OtherGroupId,
                CourseId = setup.CourseId,
            });
            await db.SaveChangesAsync();
        }

        var week = await CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        await CreateAssignmentLinkAsync(
            "Hela kursen",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = from.AddDays(1) });
        await CreateAssignmentLinkAsync(
            "Bara andra gruppen",
            new
            {
                groupId = setup.OtherGroupId,
                courseId = setup.CourseId,
                moduleId = week.Id,
                dueAt = from.AddDays(2)
            });

        var items = await GetCalendarAsync(setup.StudentId, from, to);

        Assert.Equal(["Hela kursen"], items.Select(item => item.Title));
    }

    [Fact]
    public async Task ItemsOutsideTheRange_AreExcluded()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        await CreateAssignmentLinkAsync(
            "Före", new { courseId = setup.CourseId, moduleId = week.Id, dueAt = from.AddDays(-1) });
        await CreateAssignmentLinkAsync(
            "Efter", new { courseId = setup.CourseId, moduleId = week.Id, dueAt = to.AddDays(1) });

        var items = await GetCalendarAsync(setup.StudentId, from, to);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Student_GetsNoPublishItems_AndNoDeadlinesForUnpublishedContent()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var futureWeek = await CreateModuleAsync(setup.CourseId, StartOfToday().AddDays(2));
        var from = StartOfToday();
        var to = from.AddDays(7);
        await CreateAssignmentLinkAsync(
            "Synlig",
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                publishAt = from.AddDays(-1),
                dueAt = from.AddDays(1)
            });
        await CreateAssignmentLinkAsync(
            "Publiceras senare",
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                publishAt = from.AddDays(2),
                dueAt = from.AddDays(3)
            });
        var draft = await CreateContentAsync(
            new { type = "assignment", title = "Utkast", bodyMd = "", status = "draft" });
        await CreateLinkAsync(
            draft.Id,
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                publishAt = from.AddDays(-1),
                dueAt = from.AddDays(3)
            });
        await CreateAssignmentLinkAsync(
            "Framtida vecka",
            new
            {
                courseId = setup.CourseId,
                moduleId = futureWeek.Id,
                publishAt = from.AddDays(-1),
                dueAt = from.AddDays(4)
            });

        var items = await GetCalendarAsync(setup.StudentId, from, to);

        var item = Assert.Single(items);
        Assert.Equal(("Synlig", "Deadline"), (item.Title, item.Kind));
    }

    [Fact]
    public async Task Teacher_GetsPublishItems_ForOwnGroupsAndCourses()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        var coursePublish = from.AddDays(2);
        var groupPublish = from.AddDays(3);
        await CreateTheoryLinkAsync(
            "Kurspublicering",
            new { courseId = setup.CourseId, moduleId = week.Id, publishAt = coursePublish });
        await CreateTheoryLinkAsync(
            "Grupppublicering",
            new
            {
                groupId = setup.GroupId,
                courseId = setup.CourseId,
                moduleId = week.Id,
                publishAt = groupPublish
            });

        var items = await GetCalendarAsync(setup.TeacherId, from, to);

        Assert.Equal([("Kurspublicering", "Publish"), ("Grupppublicering", "Publish")],
            items.Select(item => (item.Title, item.Kind)));
        var courseItem = items.Single(item => item.Title == "Kurspublicering");
        Assert.Equal((coursePublish, setup.CourseId, "Kurs"),
            (courseItem.At, courseItem.CourseId, courseItem.CourseTitle));
        var groupItem = items.Single(item => item.Title == "Grupppublicering");
        Assert.Equal((groupPublish, setup.GroupId, setup.CourseId),
            (groupItem.At, groupItem.GroupId, groupItem.CourseId));
        Assert.Empty(await GetCalendarAsync(setup.StudentId, from, to));
    }

    [Fact]
    public async Task Calendar_ShowsTheCourseToItsTeacherOutsideTheClass_AndNotToTheTeacherOfTheClass()
    {
        var setup = await AddSetupAsync();
        var week = await CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        await CreateTheoryLinkAsync(
            "Kurspublicering",
            new { courseId = setup.CourseId, moduleId = week.Id, publishAt = from.AddDays(2) });
        Guid outsideTeacher;
        Guid otherCourseTeacher;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var course = await db.Courses.SingleAsync(found => found.Id == setup.CourseId);
            var otherCourse = await db.Courses.SingleAsync(found => found.Id == setup.OtherCourseId);
            outsideTeacher = (await TestData.AddCourseTeacherAsync(db, course)).Id;
            otherCourseTeacher = (await TestData.AddCourseTeacherAsync(db, otherCourse)).Id;
        }

        var asCourseTeacher = await GetCalendarAsync(outsideTeacher, from, from.AddDays(7));
        var asOtherCourseTeacher = await GetCalendarAsync(otherCourseTeacher, from, from.AddDays(7));

        Assert.Equal(["Kurspublicering"], asCourseTeacher.Select(item => item.Title));
        Assert.Empty(asOtherCourseTeacher);
    }

    [Fact]
    public async Task InvalidRanges_ReturnBadRequest()
    {
        var from = StartOfToday();

        var same = await SendCalendarAsync(userId: null, from, from);
        var backwards = await SendCalendarAsync(userId: null, from, from.AddDays(-1));
        var tooLong = await SendCalendarAsync(userId: null, from, from.AddDays(63));

        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task ARangeOfExactlyTheMaximumLength_IsAccepted()
    {
        var from = StartOfToday();
        var response = await SendCalendarAsync(userId: null, from, from.AddDays(62));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        await TestData.AddCourseTeacherAsync(db, course, teacher);

        return new Setup(
            group.Id, otherGroup.Id, course.Id, otherCourse.Id, student.Id, teacher.Id);
    }

    private async Task<List<ItemResponse>> GetCalendarAsync(
        Guid userId, DateTimeOffset from, DateTimeOffset to)
    {
        var response = await SendCalendarAsync(userId, from, to);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ItemResponse>>())!;
    }

    private async Task<HttpResponseMessage> SendCalendarAsync(
        Guid? userId, DateTimeOffset from, DateTimeOffset to)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CalendarUrl(from, to));
        if (userId is not null)
        {
            request.Headers.Add(TestAuthHandler.Header, userId.Value.ToString());
        }

        return await _client.SendAsync(request);
    }

    private static string CalendarUrl(DateTimeOffset from, DateTimeOffset to) =>
        $"/api/calendar?from={Uri.EscapeDataString(from.ToString("O"))}"
        + $"&to={Uri.EscapeDataString(to.ToString("O"))}";

    private static DateTimeOffset StartOfToday()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
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
            new { type = "assignment", title, bodyMd = "", submissionModes = new[] { "onSite" } });
        return await CreateLinkAsync(content.Id, link);
    }

    private async Task<Guid> CreateTheoryLinkAsync(string title, object link)
    {
        var content = await CreateContentAsync(new { type = "theory", title, bodyMd = "" });
        return await CreateLinkAsync(content.Id, link);
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

public class CalendarAuthenticationTests(JwtTestWebApplicationFactory factory)
    : IClassFixture<JwtTestWebApplicationFactory>
{
    [Fact]
    public async Task CalendarWithoutToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        var from = DateTimeOffset.UtcNow.ToString("O");
        var to = DateTimeOffset.UtcNow.AddDays(7).ToString("O");
        var response = await client.GetAsync(
            $"/api/calendar?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
