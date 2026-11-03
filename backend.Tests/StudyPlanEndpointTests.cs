using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class StudyPlanEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Setup(Guid SchoolId, Guid GroupId, List<Guid> StudentIds);

    private record IdResponse(Guid Id);

    private record PlanResponse(int CompletedPoints, int TotalPoints, List<CourseResponse> Courses);

    private record CourseResponse(
        Guid CourseId,
        string Title,
        string Code,
        int? Points,
        string? StartsOn,
        string? EndsOn,
        string Status,
        string? Grade,
        int AssignmentCount,
        int SubmittedCount,
        int GradedCount,
        List<AssignmentResponse> Assignments);

    private record AssignmentResponse(
        Guid LinkId,
        string Title,
        DateTimeOffset? DueAt,
        string Status,
        string? Grade,
        string? Feedback,
        DateTimeOffset? GradedAt);

    [Fact]
    public async Task StudyPlan_ForStudent_ListsCoursesWithStatusAndPoints()
    {
        var setup = await AddGroupWithStudentsAsync(1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await AddCourseAsync(db, setup.SchoolId, setup.GroupId, "Avslutad kurs", 20,
                today.AddMonths(-2), today.AddDays(-1));
            await AddCourseAsync(db, setup.SchoolId, setup.GroupId, "Pågående kurs", 30,
                today.AddDays(-10), today.AddDays(10));
            await AddCourseAsync(db, setup.SchoolId, setup.GroupId, "Kommande kurs", 25,
                today.AddDays(5), null);
            await AddCourseAsync(db, setup.SchoolId, setup.GroupId, "Kurs utan datum", 5, null, null);
        }

        var plan = await GetPlanAsync(setup.StudentIds[0]);

        Assert.Equal((20, 80), (plan.CompletedPoints, plan.TotalPoints));
        Assert.Equal(
            ["Avslutad kurs", "Pågående kurs", "Kommande kurs", "Kurs utan datum"],
            plan.Courses.Select(course => course.Title));
        Assert.Equal(
            ["Completed", "Ongoing", "Upcoming", "Ongoing"],
            plan.Courses.Select(course => course.Status));
        Assert.All(plan.Courses, course => Assert.Null(course.Grade));
    }

    [Fact]
    public async Task StudyPlan_ListsVisibleAssignments_WithCounts()
    {
        var setup = await AddGroupWithStudentsAsync(1);
        Guid courseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            courseId = (await AddCourseAsync(db, setup.SchoolId, setup.GroupId, "DevOps", 25, null, null)).Id;
        }

        var moduleId = await CreateModuleAsync(courseId);
        await LinkAssignmentAsync(courseId, moduleId, "Laboration: CI/CD", dueAt: "2026-11-06T16:00:00Z");
        await LinkAssignmentAsync(courseId, moduleId, "Inlämningsuppgift 1");
        await LinkAssignmentAsync(courseId, moduleId, "Utkast", draft: true);
        await LinkAssignmentAsync(courseId, moduleId, "Publiceras senare", publishAt: "2099-01-01T08:00:00Z");

        var plan = await GetPlanAsync(setup.StudentIds[0]);

        var course = Assert.Single(plan.Courses);
        Assert.Equal(("DevOps", 2, 0, 0), (course.Title, course.AssignmentCount, course.SubmittedCount,
            course.GradedCount));
        Assert.Equal(
            ["Laboration: CI/CD", "Inlämningsuppgift 1"],
            course.Assignments.Select(assignment => assignment.Title));
        Assert.All(course.Assignments, assignment => Assert.Equal("NotStarted", assignment.Status));
    }

    [Fact]
    public async Task StudyPlan_ShowsOwnGradeAndFeedback_ButNotAnotherStudentsSubmission()
    {
        var setup = await AddGroupWithStudentsAsync(2);
        Guid courseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            courseId = (await AddCourseAsync(db, setup.SchoolId, setup.GroupId, "DevOps", 25, null, null)).Id;
        }

        var moduleId = await CreateModuleAsync(courseId);
        var gradedLinkId = await LinkAssignmentAsync(
            courseId, moduleId, "Laboration: CI/CD", dueAt: "2099-10-23T16:00:00Z");
        await LinkAssignmentAsync(courseId, moduleId, "Inlämningsuppgift 1");
        var studentId = setup.StudentIds[0];
        var otherStudentId = setup.StudentIds[1];

        var submissionId = await SubmitAsync(studentId, gradedLinkId);
        await GradeAsync(submissionId, "pass", "Bra jobbat");

        var plan = await GetPlanAsync(studentId);
        var course = Assert.Single(plan.Courses);
        Assert.Equal((2, 1, 1), (course.AssignmentCount, course.SubmittedCount, course.GradedCount));
        var graded = course.Assignments.Single(assignment => assignment.LinkId == gradedLinkId);
        Assert.Equal(("Graded", "Pass", "Bra jobbat"), (graded.Status, graded.Grade, graded.Feedback));
        Assert.NotNull(graded.GradedAt);

        var otherPlan = await GetPlanAsync(otherStudentId);
        var otherCourse = Assert.Single(otherPlan.Courses);
        Assert.Equal((2, 0, 0), (otherCourse.AssignmentCount, otherCourse.SubmittedCount, otherCourse.GradedCount));
        var notSubmitted = otherCourse.Assignments.Single(assignment => assignment.LinkId == gradedLinkId);
        Assert.Equal(("NotStarted", null, null), (notSubmitted.Status, notSubmitted.Grade, notSubmitted.Feedback));
    }

    [Fact]
    public async Task StudyPlan_CountsEverySubmittedAssignment_WhetherLinkedToCourseOrGroup()
    {
        var setup = await AddGroupWithStudentsAsync(1);
        Guid courseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            courseId = (await AddCourseAsync(db, setup.SchoolId, setup.GroupId, "DevOps", 25, null, null)).Id;
        }

        var moduleId = await CreateModuleAsync(courseId);
        var dueAt = "2099-11-11T16:00:00Z";
        var courseWideGraded = await LinkAssignmentAsync(courseId, moduleId, "Docker", dueAt: dueAt);
        var groupGraded = await LinkAssignmentAsync(
            courseId, moduleId, "Kubernetes", dueAt: dueAt, groupId: setup.GroupId);
        var courseWideSubmitted = await LinkAssignmentAsync(
            courseId, moduleId, "Sätt upp en CI-pipeline", dueAt: dueAt);
        var groupNotStarted = await LinkAssignmentAsync(
            courseId, moduleId, "Terraform", dueAt: dueAt, groupId: setup.GroupId);
        var courseWideNotStarted = await LinkAssignmentAsync(courseId, moduleId, "Monitorering", dueAt: dueAt);
        var studentId = setup.StudentIds[0];

        await GradeAsync(await SubmitAsync(studentId, courseWideGraded), "pass", "Bra");
        await GradeAsync(await SubmitAsync(studentId, groupGraded), "pass", "Bra");
        await SubmitAsync(studentId, courseWideSubmitted);

        var plan = await GetPlanAsync(studentId);

        var course = Assert.Single(plan.Courses);
        Assert.Equal((5, 3, 2), (course.AssignmentCount, course.SubmittedCount, course.GradedCount));
        var statusByLink = course.Assignments.ToDictionary(
            assignment => assignment.LinkId, assignment => assignment.Status);
        Assert.Equal("Graded", statusByLink[courseWideGraded]);
        Assert.Equal("Graded", statusByLink[groupGraded]);
        Assert.Equal("Submitted", statusByLink[courseWideSubmitted]);
        // Rader som skapades vid utdelning men aldrig lämnades in räknas inte.
        Assert.Equal("NotStarted", statusByLink[groupNotStarted]);
        Assert.Equal("NotStarted", statusByLink[courseWideNotStarted]);
    }

    [Fact]
    public async Task StudyPlan_ForUserWhoIsNotAStudentInAnyGroup_IsEmpty()
    {
        Guid teacherId;
        Guid studentWithoutGroupId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var school = await TestData.AddSchoolAsync(db);
            var group = await TestData.AddGroupAsync(db, school.Id);
            await AddCourseAsync(db, school.Id, group.Id, "DevOps", 25, null, null);
            teacherId = (await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher)).Id;
            studentWithoutGroupId = (await TestData.AddUserAsync(db, school.Id, UserRole.Student)).Id;
        }

        var teacherPlan = await GetPlanAsync(teacherId);
        var studentPlan = await GetPlanAsync(studentWithoutGroupId);

        Assert.Equal((0, 0, 0), (teacherPlan.CompletedPoints, teacherPlan.TotalPoints, teacherPlan.Courses.Count));
        Assert.Equal((0, 0, 0), (studentPlan.CompletedPoints, studentPlan.TotalPoints, studentPlan.Courses.Count));
    }

    private async Task<PlanResponse> GetPlanAsync(Guid userId)
    {
        using var student = As(userId);
        return (await student.GetFromJsonAsync<PlanResponse>("/api/me/study-plan"))!;
    }

    private HttpClient As(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userId.ToString());
        return client;
    }

    private async Task<Setup> AddGroupWithStudentsAsync(int students)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var studentIds = new List<Guid>();
        for (var index = 0; index < students; index++)
        {
            studentIds.Add((await TestData.AddMemberAsync(db, group)).Id);
        }

        return new Setup(school.Id, group.Id, studentIds);
    }

    private static async Task<Course> AddCourseAsync(
        AppDb db,
        Guid schoolId,
        Guid groupId,
        string title,
        int? points,
        DateOnly? startsOn,
        DateOnly? endsOn)
    {
        var course = await TestData.AddCourseAsync(db, schoolId, groupId);
        course.Title = title;
        course.Points = points;
        course.StartsOn = startsOn;
        course.EndsOn = endsOn;
        await db.SaveChangesAsync();
        return course;
    }

    private async Task<Guid> CreateModuleAsync(Guid courseId)
    {
        var response = await _client.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title = "Vecka 1" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> LinkAssignmentAsync(
        Guid courseId,
        Guid moduleId,
        string title,
        string? publishAt = null,
        string? dueAt = null,
        bool draft = false,
        Guid? groupId = null)
    {
        var content = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title,
            bodyMd = "",
            status = draft
                ? "draft"
                : "published",
            gradeScale = "passFail",
            submissionModes = new[] { "text" }
        });
        content.EnsureSuccessStatusCode();
        var contentId = (await content.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var link = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", new
        {
            courseId,
            groupId,
            moduleId,
            publishAt,
            dueAt
        });
        link.EnsureSuccessStatusCode();
        return (await link.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> SubmitAsync(Guid studentId, Guid linkId)
    {
        using var student = As(studentId);
        var form = new MultipartFormDataContent { { new StringContent("Mitt svar"), "bodyMd" } };
        var response = await student.PostAsync($"/api/links/{linkId}/submissions", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task GradeAsync(Guid submissionId, string level, string feedback)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/submissions/{submissionId}/grade", new { level, feedback });
        response.EnsureSuccessStatusCode();
    }
}

// Den riktiga autentiseringen, utan testhandlern: ett anrop utan token ska få 401.
public class StudyPlanAuthTests(JwtTestWebApplicationFactory factory) : IClassFixture<JwtTestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task StudyPlan_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/me/study-plan");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
