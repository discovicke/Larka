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

    [Fact]
    public async Task Student_SeesDeadlinesOfOwnGroupsAndCoursesLinksInRange_Only()
    {
        var setup = await AddSetupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        var courseDeadline = from.AddDays(1).AddHours(12);
        var groupDeadline = from.AddDays(2).AddHours(8);
        await _client.CreateAssignmentLinkAsync(
            "Kursuppgift",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = courseDeadline }, "onSite");
        await _client.CreateAssignmentLinkAsync(
            "Gruppuppgift",
            new { groupId = setup.GroupId, dueAt = groupDeadline }, "onSite");
        await _client.CreateAssignmentLinkAsync(
            "Annan grupp",
            new
            {
                groupId = setup.OtherGroupId,
                courseId = setup.OtherCourseId,
                dueAt = from.AddDays(2)
            }, "onSite");
        await _client.CreateAssignmentLinkAsync(
            "Utanför",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = to.AddDays(2) }, "onSite");

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
    public async Task Student_SeesResubmitDeadlines_AndOnlyTheirOwnPersonalDeadline()
    {
        var setup = await AddSetupAsync();
        var from = StartOfToday();
        var to = from.AddDays(14);
        var resubmit = from.AddDays(3).AddHours(12);
        var personal = from.AddDays(9).AddHours(10);
        // Huvuddeadlinen ligger före intervallet. Bara kompletteringen och den egna deadlinen hamnar i det.
        var linkId = await _client.CreateAssignmentLinkAsync(
            "Uppgift",
            new { groupId = setup.GroupId, dueAt = from.AddDays(-5), resubmitDueAt = new[] { resubmit } }, "onSite");
        Guid classmateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var group = await db.Groups.SingleAsync(row => row.Id == setup.GroupId);
            classmateId = (await TestData.AddMemberAsync(db, group)).Id;
            // Raden skapas när uppgiften delas ut, eller här om den inte redan finns.
            var submission = await db.Submissions.SingleOrDefaultAsync(row =>
                row.ContentLinkId == linkId && row.StudentId == setup.StudentId);
            if (submission is null)
            {
                submission = new Backend.Domain.Entities.Submission
                {
                    ContentLinkId = linkId,
                    StudentId = setup.StudentId
                };
                db.Submissions.Add(submission);
            }

            submission.PersonalDueAt = personal;
            await db.SaveChangesAsync();
        }

        var mine = await GetCalendarAsync(setup.StudentId, from, to);
        var classmate = await GetCalendarAsync(classmateId, from, to);

        Assert.Equal(
            [("ResubmitDeadline", resubmit), ("PersonalDeadline", personal)],
            mine.Select(item => (item.Kind, item.At)));
        Assert.Equal([("ResubmitDeadline", resubmit)], classmate.Select(item => (item.Kind, item.At)));
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

        var week = await _client.CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        await _client.CreateAssignmentLinkAsync(
            "Hela kursen",
            new { courseId = setup.CourseId, moduleId = week.Id, dueAt = from.AddDays(1) }, "onSite");
        await _client.CreateAssignmentLinkAsync(
            "Bara andra gruppen",
            new
            {
                groupId = setup.OtherGroupId,
                courseId = setup.CourseId,
                moduleId = week.Id,
                dueAt = from.AddDays(2)
            }, "onSite");

        var items = await GetCalendarAsync(setup.StudentId, from, to);

        Assert.Equal(["Hela kursen"], items.Select(item => item.Title));
    }

    [Fact]
    public async Task ItemsOutsideTheRange_AreExcluded()
    {
        var setup = await AddSetupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        await _client.CreateAssignmentLinkAsync(
            "Före", new { courseId = setup.CourseId, moduleId = week.Id, dueAt = from.AddDays(-1) }, "onSite");
        await _client.CreateAssignmentLinkAsync(
            "Efter", new { courseId = setup.CourseId, moduleId = week.Id, dueAt = to.AddDays(1) }, "onSite");

        var items = await GetCalendarAsync(setup.StudentId, from, to);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Student_GetsNoPublishItems_AndNoDeadlinesForUnpublishedContent()
    {
        var setup = await AddSetupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var futureWeek = await _client.CreateModuleAsync(setup.CourseId, publishAt: StartOfToday().AddDays(2));
        var from = StartOfToday();
        var to = from.AddDays(7);
        await _client.CreateAssignmentLinkAsync(
            "Synlig",
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                publishAt = from.AddDays(-1),
                dueAt = from.AddDays(1)
            }, "onSite");
        await _client.CreateAssignmentLinkAsync(
            "Publiceras senare",
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                publishAt = from.AddDays(2),
                dueAt = from.AddDays(3)
            }, "onSite");
        var draft = await _client.CreateContentAsync(
            new { type = "assignment", title = "Utkast", bodyMd = "", status = "draft" });
        await _client.CreateLinkAsync(
            draft.Id,
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                publishAt = from.AddDays(-1),
                dueAt = from.AddDays(3)
            });
        await _client.CreateAssignmentLinkAsync(
            "Framtida vecka",
            new
            {
                courseId = setup.CourseId,
                moduleId = futureWeek.Id,
                publishAt = from.AddDays(-1),
                dueAt = from.AddDays(4)
            }, "onSite");

        var items = await GetCalendarAsync(setup.StudentId, from, to);

        var item = Assert.Single(items);
        Assert.Equal(("Synlig", "Deadline"), (item.Title, item.Kind));
    }

    [Fact]
    public async Task Teacher_GetsPublishItems_ForOwnGroupsAndCourses()
    {
        var setup = await AddSetupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        var to = from.AddDays(7);
        var coursePublish = from.AddDays(2);
        var groupPublish = from.AddDays(3);
        await _client.CreateTheoryLinkAsync(
            "Kurspublicering",
            new { courseId = setup.CourseId, moduleId = week.Id, publishAt = coursePublish });
        await _client.CreateTheoryLinkAsync(
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
    public async Task Teacher_GetsPublishItems_AtTheEffectiveTime_AndNeverForDrafts()
    {
        var setup = await AddSetupAsync();
        var from = StartOfToday();
        var to = from.AddDays(7);
        var openWeek = await _client.CreateModuleAsync(setup.CourseId);
        var laterWeek = await _client.CreateModuleAsync(setup.CourseId, publishAt: from.AddDays(4));
        var draft = await _client.CreateContentAsync(
            new { type = "theory", title = "Utkast", bodyMd = "", status = "draft" });
        await _client.CreateLinkAsync(
            draft.Id, new { courseId = setup.CourseId, moduleId = openWeek.Id, publishAt = from.AddDays(1) });
        await _client.CreateTheoryLinkAsync(
            "Vecka öppnar senare",
            new { courseId = setup.CourseId, moduleId = laterWeek.Id, publishAt = from.AddDays(-3) });
        await _client.CreateTheoryLinkAsync(
            "Del efter veckan",
            new { courseId = setup.CourseId, moduleId = laterWeek.Id, publishAt = from.AddDays(5) });

        var items = await GetCalendarAsync(setup.TeacherId, from, to);

        Assert.Equal(
            [("Vecka öppnar senare", from.AddDays(4)), ("Del efter veckan", from.AddDays(5))],
            items.Where(item => item.Kind == "Publish").Select(item => (item.Title, item.At)));
    }

    [Fact]
    public async Task Calendar_ShowsTheCourseToItsTeacherOutsideTheClass_AndNotToTheTeacherOfTheClass()
    {
        var setup = await AddSetupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var from = StartOfToday();
        await _client.CreateTheoryLinkAsync(
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
        var group = await TestData.AddGroupAsync(db, school.Id, placeholderTeaches: true);
        var otherGroup = await TestData.AddGroupAsync(db, school.Id, placeholderTeaches: true);
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
