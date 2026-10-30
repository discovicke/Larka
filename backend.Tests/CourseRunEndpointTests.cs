using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class CourseRunEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record RunResponse(Guid CourseId, Guid GroupId, int ModuleCount, int PartCount, int StaffCount);

    // Källkursen läses av klassen NET25 med en elev. Läraren är lärare i kursen utan att vara med i klassen, och
    // klasslärare är lärare i klassen men inte i kursen. Veckan har en teori och en inlämning
    // som eleven har lämnat in och fått betyg på.
    private record Scenario(
        Guid SchoolId,
        Guid CourseId,
        Guid TeacherId,
        Guid ClassTeacherId,
        Guid StudentId,
        Guid NewGroupId,
        Guid SourceAssignmentLink);

    private static readonly DateTimeOffset _due = new(2026, 2, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Run_AsAdmin_CreatesTheCourseWithInfoFromTheSource()
    {
        var scenario = await AddScenarioAsync();

        var response = await RunAsync(scenario, Guid.Empty);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var run = (await response.Content.ReadFromJsonAsync<RunResponse>())!;
        await using var db = OpenDb();
        var course = await db.Courses.SingleAsync(found => found.Id == run.CourseId);
        Assert.Equal(
            ("Källkurs", "NET26-DEVOPS", "Beskrivning", 25),
            (course.Title, course.Code, course.Description, course.Points));
        Assert.Equal(scenario.SchoolId, course.SchoolId);
        Assert.Equal(new DateOnly(2027, 1, 4), course.StartsOn);
        Assert.Equal(new DateOnly(2027, 3, 1), course.EndsOn);
    }

    [Fact]
    public async Task Run_AsTeacherOfTheSource_Succeeds_AndCanTeachTheNewCourse()
    {
        var scenario = await AddScenarioAsync();

        var response = await RunAsync(scenario, scenario.TeacherId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var run = (await response.Content.ReadFromJsonAsync<RunResponse>())!;
        var edit = await SendAsync(HttpMethod.Put, $"/api/courses/{run.CourseId}", scenario.TeacherId,
            new
            {
                title = "Ny titel",
                description = "",
                points = (int?)null,
                startsOn = (string?)null,
                endsOn = (string?)null
            });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task Run_AsTeacherWhoCannotTeachTheSource_IsForbidden_AndCreatesNothing()
    {
        var scenario = await AddScenarioAsync();
        await using (var db = OpenDb())
        {
            var otherGroup = await TestData.AddGroupAsync(db, scenario.SchoolId);
            var otherTeacher = await TestData.AddMemberAsync(db, otherGroup, GroupRole.Teacher, UserRole.Teacher);
            var response = await RunAsync(scenario, otherTeacher.Id);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await using var check = OpenDb();
        Assert.False(await check.Courses.AnyAsync(
            course => course.SchoolId == scenario.SchoolId && course.Code == "NET26-DEVOPS"));
    }

    [Fact]
    public async Task Run_AsTeacherOfTheClassButNotOfTheCourse_IsForbidden()
    {
        var scenario = await AddScenarioAsync();

        var response = await RunAsync(scenario, scenario.ClassTeacherId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Run_WithAGroupFromAnotherSchool_ReturnsBadRequest_AndCreatesNothing()
    {
        var scenario = await AddScenarioAsync();
        await using var db = OpenDb();
        var foreign = await TestData.AddGroupAsync(db);

        var response = await RunAsync(scenario, Guid.Empty, groupId: foreign.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await db.Courses.AnyAsync(
            course => course.SchoolId == scenario.SchoolId && course.Code == "NET26-DEVOPS"));
        Assert.False(await db.GroupCourses.AnyAsync(groupCourse => groupCourse.GroupId == foreign.Id));
    }

    [Fact]
    public async Task Run_WithMissingCourseOrGroup_ReturnsNotFound()
    {
        var scenario = await AddScenarioAsync();

        var course = await SendAsync(
            HttpMethod.Post, $"/api/courses/{Guid.NewGuid()}/runs", Guid.Empty, Body(scenario));
        var group = await RunAsync(scenario, Guid.Empty, groupId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, course.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, group.StatusCode);
    }

    [Fact]
    public async Task Run_WithInvalidInput_ReturnsBadRequest()
    {
        var scenario = await AddScenarioAsync();
        await using var db = OpenDb();
        var sourceCode = (await db.Courses.SingleAsync(course => course.Id == scenario.CourseId)).Code;

        var duplicateCode = await RunAsync(scenario, Guid.Empty, code: sourceCode);
        var noCode = await RunAsync(scenario, Guid.Empty, code: " ");
        var endBeforeStart = await RunAsync(scenario, Guid.Empty, startsOn: "2027-03-01", endsOn: "2027-01-04");
        var noDates = await SendAsync(HttpMethod.Post, $"/api/courses/{scenario.CourseId}/runs", Guid.Empty,
            new { code = "X", groupId = scenario.NewGroupId, copyContent = true });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateCode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noCode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, endBeforeStart.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDates.StatusCode);
    }

    [Fact]
    public async Task Run_LinksTheChosenClassToTheNewCourse()
    {
        var scenario = await AddScenarioAsync();

        var run = await CreateRunAsync(scenario);

        await using var db = OpenDb();
        Assert.Equal(
            [scenario.NewGroupId],
            await db.GroupCourses.Where(groupCourse => groupCourse.CourseId == run.CourseId)
                .Select(groupCourse => groupCourse.GroupId).ToListAsync());
    }

    [Fact]
    public async Task Run_CopiesTheCourseTeachers_ButNoClassTeachersAndNoStudents()
    {
        var scenario = await AddScenarioAsync();

        var run = await CreateRunAsync(scenario);

        Assert.Equal(1, run.StaffCount);
        await using var db = OpenDb();
        Assert.Equal(
            [scenario.TeacherId],
            await db.CourseTeachers.Where(courseTeacher => courseTeacher.CourseId == run.CourseId)
                .Select(courseTeacher => courseTeacher.UserId).ToListAsync());
        // Klassen får varken lärare eller elever, och källkursens lärare är kvar.
        Assert.Empty(await db.Enrollments.Where(enrollment => enrollment.GroupId == scenario.NewGroupId)
            .ToListAsync());
        Assert.True(await db.CourseTeachers.AnyAsync(courseTeacher =>
            courseTeacher.CourseId == scenario.CourseId && courseTeacher.UserId == scenario.TeacherId));
        // Kursen ligger i en annan skola än adminen, så läraren hämtar listan.
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/courses/{run.CourseId}/staff");
        request.Headers.Add(TestAuthHandler.Header, scenario.TeacherId.ToString());
        var staffResponse = await _client.SendAsync(request);
        var staff = await staffResponse.Content.ReadFromJsonAsync<List<StaffResponse>>();
        Assert.Equal([scenario.TeacherId], staff!.Select(teacher => teacher.UserId));
    }

    private record StaffResponse(Guid UserId);

    [Fact]
    public async Task Run_CopiesTheWeeksAsDrafts_WithDatesShiftedByTheStartDates()
    {
        var scenario = await AddScenarioAsync();

        var run = await CreateRunAsync(scenario);

        Assert.Equal((1, 2), (run.ModuleCount, run.PartCount));
        await using var db = OpenDb();
        var week = await db.Modules.SingleAsync(module => module.CourseId == run.CourseId);
        Assert.Equal("Vecka 1", week.Title);
        // Källan startar 2026-01-05, den nya omgången 2027-01-04: 364 dagar.
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero).AddDays(364), week.PublishAt);
        var links = await db.ContentLinks.Where(link => link.CourseId == run.CourseId).ToListAsync();
        Assert.Equal(_due.AddDays(364), links.Single(link => link.DueAt != null).DueAt);
        var contentIds = links.Select(link => link.ContentId).ToList();
        var content = await db.Content.Where(found => contentIds.Contains(found.Id)).ToListAsync();
        Assert.All(content, found => Assert.Equal(ContentStatus.Draft, found.Status));
        var original = await db.ContentLinks.SingleAsync(link => link.Id == scenario.SourceAssignmentLink);
        Assert.Equal(_due, original.DueAt);
    }

    [Fact]
    public async Task Run_NeverCopiesStudentWork()
    {
        var scenario = await AddScenarioAsync();

        var run = await CreateRunAsync(scenario);

        await using var db = OpenDb();
        var linkIds = await db.ContentLinks.Where(link => link.CourseId == run.CourseId)
            .Select(link => link.Id).ToListAsync();
        Assert.Empty(await db.Submissions.Where(found => linkIds.Contains(found.ContentLinkId)).ToListAsync());
        Assert.Empty(await db.PartCompletions.Where(found => linkIds.Contains(found.ContentLinkId)).ToListAsync());
        Assert.Empty(await db.Enrollments.Where(enrollment => enrollment.GroupId == scenario.NewGroupId
            && enrollment.UserId == scenario.StudentId).ToListAsync());
        Assert.Equal(
            1,
            await db.Submissions.CountAsync(found => found.ContentLinkId == scenario.SourceAssignmentLink));
    }

    [Fact]
    public async Task Run_WithoutCopyContent_CopiesOnlyTheCourseInfoLinkAndStaff()
    {
        var scenario = await AddScenarioAsync();

        var run = await CreateRunAsync(scenario, copyContent: false);

        Assert.Equal((0, 0, 1), (run.ModuleCount, run.PartCount, run.StaffCount));
        await using var db = OpenDb();
        Assert.Empty(await db.Modules.Where(module => module.CourseId == run.CourseId).ToListAsync());
        Assert.Empty(await db.ContentLinks.Where(link => link.CourseId == run.CourseId).ToListAsync());
        Assert.True(await db.GroupCourses.AnyAsync(groupCourse => groupCourse.CourseId == run.CourseId));
    }

    [Fact]
    public async Task Run_WhenTheTeacherIsAlreadyInTheClass_LeavesTheEnrollmentAsItIs()
    {
        var scenario = await AddScenarioAsync();
        await using (var db = OpenDb())
        {
            db.Enrollments.Add(new Enrollment
            {
                UserId = scenario.TeacherId,
                GroupId = scenario.NewGroupId,
                Role = GroupRole.Teacher
            });
            await db.SaveChangesAsync();
        }

        var run = await CreateRunAsync(scenario);

        Assert.Equal(1, run.StaffCount);
        await using var check = OpenDb();
        Assert.Single(await check.Enrollments.Where(enrollment => enrollment.GroupId == scenario.NewGroupId)
            .ToListAsync());
        Assert.True(await check.CourseTeachers.AnyAsync(courseTeacher =>
            courseTeacher.CourseId == run.CourseId && courseTeacher.UserId == scenario.TeacherId));
    }

    private async Task<RunResponse> CreateRunAsync(Scenario scenario, bool copyContent = true)
    {
        var response = await RunAsync(scenario, Guid.Empty, copyContent: copyContent);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RunResponse>())!;
    }

    private static object Body(
        Scenario scenario, string code = "NET26-DEVOPS", Guid? groupId = null, string startsOn = "2027-01-04",
        string endsOn = "2027-03-01", bool copyContent = true) =>
        new { code, groupId = groupId ?? scenario.NewGroupId, startsOn, endsOn, copyContent };

    private Task<HttpResponseMessage> RunAsync(
        Scenario scenario, Guid asUser, string code = "NET26-DEVOPS", Guid? groupId = null,
        string startsOn = "2027-01-04", string endsOn = "2027-03-01", bool copyContent = true) =>
        SendAsync(HttpMethod.Post, $"/api/courses/{scenario.CourseId}/runs", asUser,
            Body(scenario, code, groupId, startsOn, endsOn, copyContent));

    // Guid.Empty betyder placeholder-användaren, som är Admin.
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Guid asUser, object body)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        if (asUser != Guid.Empty)
        {
            request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        }

        return await _client.SendAsync(request);
    }

    private AppDb OpenDb() => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDb>();

    private async Task<Scenario> AddScenarioAsync()
    {
        await using var db = OpenDb();
        var school = await TestData.AddSchoolAsync(db);
        var oldGroup = await TestData.AddGroupAsync(db, school.Id);
        var newGroup = await TestData.AddGroupAsync(db, school.Id);
        var teacher = await TestData.AddUserAsync(db, school.Id, UserRole.Teacher);
        var classTeacher = await TestData.AddMemberAsync(db, oldGroup, GroupRole.Teacher, UserRole.Teacher);
        var student = await TestData.AddMemberAsync(db, oldGroup);
        var course = await TestData.AddCourseAsync(db, school.Id, oldGroup.Id);
        await TestData.AddCourseTeacherAsync(db, course, teacher);
        course.Title = "Källkurs";
        course.Description = "Beskrivning";
        course.Points = 25;
        course.StartsOn = new DateOnly(2026, 1, 5);
        var week = new Module
        {
            CourseId = course.Id,
            Title = "Vecka 1",
            Position = 1,
            PublishAt = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero)
        };
        db.Modules.Add(week);
        var theory = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Theory,
            Title = "Teori",
            Status = ContentStatus.Published
        };
        var assignment = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Assignment,
            Title = "Inlämning",
            Status = ContentStatus.Published,
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFail,
                SubmissionModes = [SubmissionMode.Text],
                CompletionRule = CompletionRule.Approved
            }
        };
        db.Content.AddRange(theory, assignment);
        var theoryLink = new ContentLink
        {
            ContentId = theory.Id,
            CourseId = course.Id,
            ModuleId = week.Id,
            Position = 1
        };
        var assignmentLink = new ContentLink
        {
            ContentId = assignment.Id,
            CourseId = course.Id,
            ModuleId = week.Id,
            Position = 2,
            DueAt = _due
        };
        db.ContentLinks.AddRange(theoryLink, assignmentLink);
        var submission = new Submission
        {
            ContentLinkId = assignmentLink.Id,
            StudentId = student.Id,
            Status = SubmissionStatus.Submitted,
            SubmittedAt = _due
        };
        db.Submissions.Add(submission);
        db.PartCompletions.Add(new PartCompletion { ContentLinkId = theoryLink.Id, StudentId = student.Id });
        await db.SaveChangesAsync();
        return new Scenario(
            school.Id, course.Id, teacher.Id, classTeacher.Id, student.Id, newGroup.Id, assignmentLink.Id);
    }
}
