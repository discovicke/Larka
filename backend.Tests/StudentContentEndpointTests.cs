using System.Net;
using System.Net.Http.Json;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class StudentContentEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Setup(Guid CourseId, Guid GroupId, List<Guid> StudentIds);

    private record SubmissionResponse(Guid Id, Guid StudentId, string Status);

    private record BlockerResponse(Guid LinkId, string? Title, bool IsSatisfied);

    private record RepoResponse(
        string RepoFullName,
        string? LastSha,
        string CheckStatus,
        DateTimeOffset UpdatedAt,
        string ProvisioningState,
        string? ProvisioningError,
        string? SubmissionSha,
        DateTimeOffset? LockedAt,
        string? LockError);

    private record ItemResponse(
        Guid LinkId,
        Guid ContentId,
        string Type,
        string? GradeScale,
        string Title,
        string? BodyMd,
        bool IsLocked,
        bool IsCompleted,
        bool CanMarkDone,
        List<BlockerResponse> BlockedBy,
        string? SubmissionStatus,
        string? GradeLevel,
        bool HasRepoMode,
        List<string> SubmissionModes,
        RepoResponse? Repo,
        List<RelatedLinkResponse>? RelatedLinks);

    private record RelatedLinkResponse(
        Guid LinkId, Guid ContentId, string Title, string Type, Guid? ModuleId, string? ModuleTitle, Guid? CourseId);

    private record UnlinkedStudentResponse(Guid Id, string Name, string Email);

    private record ModuleWithContent(
        Guid Id,
        string Title,
        int Position,
        DateTimeOffset? PublishAt,
        bool IsAvailable,
        List<ItemResponse> Content);

    [Fact]
    public async Task StudentModules_ShowAvailableAndLockedParts_AndHideUnpublished()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var theory = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A", "Hemlig text");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (theory, []), (exercise, [theory]));
        // Läggs till efter roadmapen, så att den fortfarande är ett utkast.
        await LinkContentAsync(setup.CourseId, week.Id, new
        {
            type = "theory",
            title = "Utkast",
            bodyMd = "Inte klar",
            status = "draft"
        });

        var modules = await GetStudentModulesAsync(setup, setup.StudentIds[0]);

        var content = Assert.Single(modules).Content;
        Assert.Equal(["Teori A", "Övning B"], content.Select(item => item.Title));
        var available = content[0];
        Assert.False(available.IsLocked);
        Assert.True(available.CanMarkDone);
        Assert.Equal("Hemlig text", available.BodyMd);
        var locked = content[1];
        Assert.True(locked.IsLocked);
        Assert.Null(locked.BodyMd);
        Assert.False(locked.CanMarkDone);
        var blocker = Assert.Single(locked.BlockedBy);
        Assert.Equal(("Teori A", false), (blocker.Title, blocker.IsSatisfied));
    }

    [Fact]
    public async Task StudentModules_IncludeRelatedTheoryLinks()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var theoryLink = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A", "Läs det här");
        var assignment = await _client.CreateContentAsync(new
        {
            type = "assignment",
            title = "Uppgift",
            bodyMd = "",
            gradeScale = "passFail",
            submissionModes = new[] { "text" }
        });
        var assignmentLink = (await _client.PostJsonAsync<LinkResponse>(
            $"/api/content/{assignment.Id}/links",
            new
            {
                courseId = setup.CourseId,
                moduleId = week.Id,
                relatedLinkIds = new[] { theoryLink }
            })).Id;

        var modules = await GetStudentModulesAsync(setup, setup.StudentIds[0]);

        var item = Assert.Single(modules).Content.Single(content => content.LinkId == assignmentLink);
        var related = Assert.Single(item.RelatedLinks!);
        Assert.Equal(theoryLink, related.LinkId);
        Assert.Equal("Teori A", related.Title);
        Assert.Equal("Vecka 1", related.ModuleTitle);
    }

    [Fact]
    public async Task LockedPart_KeepsItsTitle_ButTheBodyTextIsNotInTheResponse()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var theory = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A");
        var locked = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori B", "Texten som är låst");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (theory, []), (locked, [theory]));

        using var student = StudentClient(setup.StudentIds[0]);
        var raw = await student.GetStringAsync($"/api/courses/{setup.CourseId}/student-modules");

        Assert.Contains("Teori B", raw);
        Assert.DoesNotContain("Texten som är låst", raw);
    }

    [Fact]
    public async Task FutureWeek_IsAShellWithoutContent_AndCannotBeCompleted()
    {
        var setup = await AddCourseWithGroupAsync();
        var publishAt = "2099-01-05T08:00:00Z";
        var week = await _client.CreateModuleAsync(setup.CourseId, publishAt: publishAt);
        var theory = await LinkTheoryAsync(setup.CourseId, week.Id, "Hemlig teori");

        var module = Assert.Single(await GetStudentModulesAsync(setup, setup.StudentIds[0]));
        Assert.False(module.IsAvailable);
        Assert.Equal(DateTimeOffset.Parse(publishAt), module.PublishAt);
        Assert.Empty(module.Content);

        var response = await SetCompletionAsync(setup, setup.StudentIds[0], theory, true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_CannotCompleteALockedPart()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var theory = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A");
        var lockedTheory = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (theory, []), (lockedTheory, [theory]));

        var response = await SetCompletionAsync(setup, setup.StudentIds[0], lockedTheory, true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Teori A", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CompletingATheory_UnlocksTheNextPartInTheSameResponse()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var theory = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (theory, []), (exercise, [theory]));

        var response = await SetCompletionAsync(setup, setup.StudentIds[0], theory, true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.True(saved.Content.Single(item => item.LinkId == theory).IsCompleted);
        Assert.False(saved.Content.Single(item => item.LinkId == exercise).IsLocked);
        var listed = (await GetStudentModulesAsync(setup, setup.StudentIds[0])).Single();
        Assert.False(listed.Content.Single(item => item.LinkId == exercise).IsLocked);
    }

    [Fact]
    public async Task UnmarkingATheory_LocksTheDependentPartAgain()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var theory = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (theory, []), (exercise, [theory]));
        await SetCompletionAsync(setup, setup.StudentIds[0], theory, true);

        var response = await SetCompletionAsync(setup, setup.StudentIds[0], theory, false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.True(saved.Content.Single(item => item.LinkId == exercise).IsLocked);
    }

    [Fact]
    public async Task SubmittedAssignment_UnlocksWithTheDefaultRule()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, "Inlämning");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (assignment, []), (exercise, [assignment]));

        await SubmitTextAsync(setup, setup.StudentIds[0], assignment, "Mitt svar");

        var modules = await GetStudentModulesAsync(setup, setup.StudentIds[0]);
        var content = modules.Single().Content;
        Assert.True(content.Single(item => item.LinkId == assignment).IsCompleted);
        Assert.False(content.Single(item => item.LinkId == exercise).IsLocked);
    }

    [Fact]
    public async Task AutomaticFail_CountsAsFinished_AndUnlocksTheNextPart()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, "Inlämning");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (assignment, []), (exercise, [assignment]));

        var before = (await GetStudentModulesAsync(setup, setup.StudentIds[0])).Single().Content;
        Assert.False(before.Single(item => item.LinkId == assignment).IsCompleted);
        Assert.True(before.Single(item => item.LinkId == exercise).IsLocked);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var link = await db.ContentLinks.FindAsync(assignment);
            link!.DueAt = DateTimeOffset.UtcNow.AddDays(-2);
            link.ResubmitDueAt = [];
            await db.SaveChangesAsync();
        }

        var after = (await GetStudentModulesAsync(setup, setup.StudentIds[0])).Single().Content;
        Assert.True(after.Single(item => item.LinkId == assignment).IsCompleted);
        Assert.False(after.Single(item => item.LinkId == exercise).IsLocked);
    }

    [Fact]
    public async Task AssignmentItem_ListsItsSubmissionModes()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, "Inlämning");

        var item = (await GetStudentModulesAsync(setup, setup.StudentIds[0]))
            .Single().Content.Single(item => item.LinkId == assignment);

        Assert.NotEmpty(item.SubmissionModes);
    }

    [Fact]
    public async Task ApprovedAssignment_UnlocksFirstAfterAGrade()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, "Inlämning");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRulesAsync(setup.CourseId, week.Id, (assignment, [], "Approved"), (exercise, [assignment], null));
        var studentId = setup.StudentIds[0];

        var submission = await SubmitTextAsync(setup, studentId, assignment, "Mitt svar");
        var afterSubmit = (await GetStudentModulesAsync(setup, studentId)).Single().Content;
        Assert.True(afterSubmit.Single(item => item.LinkId == exercise).IsLocked);
        Assert.False(afterSubmit.Single(item => item.LinkId == assignment).IsCompleted);

        var grade = await _client.PostAsJsonAsync(
            $"/api/submissions/{submission.Id}/grade", new { level = "pass", feedback = "Bra" });
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);

        var afterGrade = (await GetStudentModulesAsync(setup, studentId)).Single().Content;
        Assert.False(afterGrade.Single(item => item.LinkId == exercise).IsLocked);
        Assert.True(afterGrade.Single(item => item.LinkId == assignment).IsCompleted);
        Assert.Equal("Pass", afterGrade.Single(item => item.LinkId == assignment).GradeLevel);
    }

    [Fact]
    public async Task ApprovedAssignment_ThatNeedsWork_DoesNotUnlock()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, "Inlämning");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRulesAsync(setup.CourseId, week.Id, (assignment, [], "Approved"), (exercise, [assignment], null));
        var studentId = setup.StudentIds[0];

        var submission = await SubmitTextAsync(setup, studentId, assignment, "Mitt svar");
        await _client.PostAsJsonAsync(
            $"/api/submissions/{submission.Id}/grade", new { outcome = "needsWork", feedback = "Komplettera" });

        var content = (await GetStudentModulesAsync(setup, studentId)).Single().Content;
        Assert.True(content.Single(item => item.LinkId == exercise).IsLocked);
    }

    [Fact]
    public async Task Student_CannotManuallyCompleteAnAssignmentWithSubmissionModes()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, "Inlämning");

        var response = await SetCompletionAsync(setup, setup.StudentIds[0], assignment, true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("handing in", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AClassmatesCompletion_DoesNotUnlockForOtherStudents()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var theory = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (theory, []), (exercise, [theory]));

        await SetCompletionAsync(setup, setup.StudentIds[0], theory, true);

        var content = (await GetStudentModulesAsync(setup, setup.StudentIds[1])).Single().Content;
        Assert.True(content.Single(item => item.LinkId == exercise).IsLocked);
    }

    [Fact]
    public async Task StudentModules_ForAStudentInAnotherGroup_ReturnForbidden()
    {
        var setup = await AddCourseWithGroupAsync();
        Guid outsider;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var otherGroup = await TestData.AddGroupAsync(db);
            outsider = (await TestData.AddMemberAsync(db, otherGroup)).Id;
        }

        using var student = StudentClient(outsider);
        var view = await student.GetAsync($"/api/courses/{setup.CourseId}/student-modules");
        var completion = await student.PutAsJsonAsync(
            $"/api/courses/{setup.CourseId}/links/{Guid.NewGuid()}/completion", new { completed = true });

        Assert.Equal(HttpStatusCode.Forbidden, view.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, completion.StatusCode);
    }

    [Fact]
    public async Task StudentModules_ForATeacher_ReturnForbidden()
    {
        var setup = await AddCourseWithGroupAsync();
        Guid teacherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var group = await db.Groups.FindAsync(setup.GroupId);
            teacherId = (await TestData.AddMemberAsync(db, group!, GroupRole.Teacher, UserRole.Teacher)).Id;
        }

        using var teacher = StudentClient(teacherId);
        var response = await teacher.GetAsync($"/api/courses/{setup.CourseId}/student-modules");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StudentModules_ForAnUnknownCourse_ReturnNotFound()
    {
        var setup = await AddCourseWithGroupAsync();

        using var student = StudentClient(setup.StudentIds[0]);
        var response = await student.GetAsync($"/api/courses/{Guid.NewGuid()}/student-modules");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StudentModules_InTwoGroups_ShowTheUnionOfTheGroupsParts()
    {
        var setup = await AddCourseWithGroupAsync();
        Guid studentId;
        Guid otherGroup;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var schoolId = (await db.Courses.FindAsync(setup.CourseId))!.SchoolId;
            var group = await TestData.AddGroupAsync(db, schoolId);
            otherGroup = group.Id;
            db.GroupCourses.Add(new GroupCourse { GroupId = otherGroup, CourseId = setup.CourseId });
            studentId = (await TestData.AddMemberAsync(db, group)).Id;
            db.Enrollments.Add(new Enrollment
            {
                UserId = studentId,
                GroupId = setup.GroupId,
                Role = GroupRole.Student
            });
            await db.SaveChangesAsync();
        }

        var week = await _client.CreateModuleAsync(setup.CourseId);
        await LinkTheoryAsync(setup.CourseId, week.Id, "Gemensam");
        var content = await _client.CreateContentAsync(
            new { type = "theory", title = "Bara andra gruppen", bodyMd = "" });
        await _client.PostJsonAsync($"/api/content/{content.Id}/links",
            new { groupId = otherGroup, courseId = setup.CourseId, moduleId = week.Id });

        var modules = await GetStudentModulesAsync(setup, studentId);

        Assert.Equal(
            ["Bara andra gruppen", "Gemensam"],
            modules.Single().Content.Select(item => item.Title).Order());
    }

    [Fact]
    public async Task ScheduledBlocker_KeepsThePartLockedWithoutRevealingItsTitle()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var scheduled = await LinkTheoryAsync(setup.CourseId, week.Id, "Publiceras senare", null,
            "2099-01-01T08:00:00Z");
        var exercise = await LinkExerciseAsync(setup.CourseId, week.Id, "Övning B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (scheduled, []), (exercise, [scheduled]));

        var content = (await GetStudentModulesAsync(setup, setup.StudentIds[0])).Single().Content;

        var item = Assert.Single(content);
        Assert.True(item.IsLocked);
        Assert.Null(Assert.Single(item.BlockedBy).Title);
    }

    [Fact]
    public async Task Completion_OfAPartThatDoesNotBelongToTheGroup_ReturnsNotFound()
    {
        var setup = await AddCourseWithGroupAsync();
        Guid otherGroup;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var schoolId = (await db.Courses.FindAsync(setup.CourseId))!.SchoolId;
            otherGroup = (await TestData.AddGroupAsync(db, schoolId)).Id;
            db.GroupCourses.Add(new GroupCourse { GroupId = otherGroup, CourseId = setup.CourseId });
            await db.SaveChangesAsync();
        }

        var week = await _client.CreateModuleAsync(setup.CourseId);
        var content = await _client.CreateContentAsync(new { type = "theory", title = "Bara för andra", bodyMd = "" });
        var link = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links",
            new { groupId = otherGroup, courseId = setup.CourseId, moduleId = week.Id });
        var linkId = (await link.Content.ReadFromJsonAsync<LinkResponse>())!.Id;

        var response = await SetCompletionAsync(setup, setup.StudentIds[0], linkId, true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_CannotCompleteANewsPart()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var news = await LinkContentAsync(setup.CourseId, week.Id, new
        {
            type = "news",
            title = "Nyhet",
            bodyMd = "Dagens nyhet"
        });

        var response = await SetCompletionAsync(setup, setup.StudentIds[0], news, true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("news", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeletingAPart_RemovesItsDependencies()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var first = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A");
        var second = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori B");
        await SaveRoadmapAsync(setup.CourseId, week.Id, (first, []), (second, [first]));

        Guid contentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            contentId = (await db.ContentLinks.FindAsync(first))!.ContentId;
        }

        var deleted = await _client.DeleteAsync($"/api/content/{contentId}/links/{first}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var content = (await GetStudentModulesAsync(setup, setup.StudentIds[0])).Single().Content;
        var remaining = Assert.Single(content);
        Assert.Equal(second, remaining.LinkId);
        Assert.False(remaining.IsLocked);
        Assert.Empty(remaining.BlockedBy);
    }

    [Fact]
    public async Task DeletingAPart_WithACompletion_ReturnsConflict()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var first = await LinkTheoryAsync(setup.CourseId, week.Id, "Teori A");
        await SetCompletionAsync(setup, setup.StudentIds[0], first, true);

        Guid contentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            contentId = (await db.ContentLinks.FindAsync(first))!.ContentId;
        }

        var deleted = await _client.DeleteAsync($"/api/content/{contentId}/links/{first}");

        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Single((await GetStudentModulesAsync(setup, setup.StudentIds[0])).Single().Content);
    }

    [Fact]
    public async Task RepoAssignment_IncludesRepoAndRepoModeForStudent()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        await LinkContentAsync(setup.CourseId, week.Id, new
        {
            type = "assignment",
            title = "Repo-uppgift",
            bodyMd = "",
            submissionModes = new[] { "repo" },
            templateRepo = "skol-org/template"
        });

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<GitHubRepoProvisioningService>()
                .ProcessPendingAsync(CancellationToken.None);
        }

        var item = Assert.Single(
            (await GetStudentModulesAsync(setup, setup.StudentIds[0])).Single().Content);

        Assert.True(item.HasRepoMode);
        Assert.NotNull(item.Repo);
        Assert.StartsWith("skol-org/", item.Repo.RepoFullName);
        Assert.Equal("Ready", item.Repo.ProvisioningState);
        Assert.Equal("Pending", item.Repo.CheckStatus);
    }

    [Fact]
    public async Task UnlinkedGitHubStudents_ReturnsOnlyStudentsWithoutLogin()
    {
        var setup = await AddCourseWithGroupAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var linked = await db.Users.FindAsync(setup.StudentIds[0]);
            linked!.GitHubLogin = "alice";
            await db.SaveChangesAsync();
        }

        var students = await _client.GetFromJsonAsync<List<UnlinkedStudentResponse>>(
            $"/api/courses/{setup.CourseId}/students/unlinked-github");

        var student = Assert.Single(students!);
        Assert.Equal(setup.StudentIds[1], student.Id);
    }

    private record ChecklistResponse(List<int> CheckedIndexes);

    private record StudentHelpResponse(bool Requested, string? Message, bool Resolved);

    [Fact]
    public async Task Checklist_RoundTripsPerStudent_AndOtherStudentsDoNotSeeIt()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var link = await LinkAssignmentAsync(setup.CourseId, week.Id, "Uppgift");

        Assert.Empty((await GetChecklistAsync(setup, setup.StudentIds[0], link)).CheckedIndexes);

        (await SetChecklistAsync(setup, setup.StudentIds[0], link, 2, true)).EnsureSuccessStatusCode();
        (await SetChecklistAsync(setup, setup.StudentIds[0], link, 0, true)).EnsureSuccessStatusCode();
        // Samma värde två gånger ger fortfarande en rad.
        (await SetChecklistAsync(setup, setup.StudentIds[0], link, 0, true)).EnsureSuccessStatusCode();

        Assert.Equal([0, 2], (await GetChecklistAsync(setup, setup.StudentIds[0], link)).CheckedIndexes);
        Assert.Empty((await GetChecklistAsync(setup, setup.StudentIds[1], link)).CheckedIndexes);

        var cleared = await SetChecklistAsync(setup, setup.StudentIds[0], link, 0, false);
        Assert.Equal([2], (await cleared.Content.ReadFromJsonAsync<ChecklistResponse>())!.CheckedIndexes);
    }

    [Fact]
    public async Task StudentHelpFlag_CanBeRequestedAndResolved()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var link = await LinkAssignmentAsync(setup.CourseId, week.Id, "Uppgift");
        var studentId = setup.StudentIds[0];
        Guid teacherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var teacher = await TestData.AddUserAsync(db, role: UserRole.Teacher);
            db.CourseTeachers.Add(new CourseTeacher
            {
                CourseId = setup.CourseId,
                UserId = teacher.Id
            });
            await db.SaveChangesAsync();
            teacherId = teacher.Id;
        }

        using var student = StudentClient(studentId);
        var requested = await student.PostAsJsonAsync(
            $"/api/links/{link}/help", new { message = "Jag fastnar på releaseflödet." });
        requested.EnsureSuccessStatusCode();
        var status = await requested.Content.ReadFromJsonAsync<StudentHelpResponse>();
        Assert.True(status!.Requested);
        Assert.Equal("Jag fastnar på releaseflödet.", status.Message);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var notification = db.Notifications.Single(item =>
                item.UserId == teacherId && item.Kind == NotificationKind.StudentHelpRequested);
            Assert.NotEqual(Guid.Empty, notification.ContentId);
        }

        var resolved = await _client.PostAsync(
            $"/api/links/{link}/help/{studentId}/resolve", null);
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);

        var after = await student.GetFromJsonAsync<StudentHelpResponse>($"/api/links/{link}/help");
        Assert.False(after!.Requested);
        Assert.True(after.Resolved);
    }

    [Fact]
    public async Task Checklist_WithNegativeOrHugeIndex_ReturnsBadRequest()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var link = await LinkAssignmentAsync(setup.CourseId, week.Id, "Uppgift");

        var negative = await SetChecklistAsync(setup, setup.StudentIds[0], link, -1, true);
        var huge = await SetChecklistAsync(setup, setup.StudentIds[0], link, 100000, true);

        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);
    }

    [Fact]
    public async Task Checklist_ForAStudentInAnotherGroup_ReturnsForbidden()
    {
        var setup = await AddCourseWithGroupAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId);
        var link = await LinkAssignmentAsync(setup.CourseId, week.Id, "Uppgift");
        Guid outsider;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var otherGroup = await TestData.AddGroupAsync(db);
            outsider = (await TestData.AddMemberAsync(db, otherGroup)).Id;
        }

        var read = await StudentClient(outsider)
            .GetAsync($"/api/courses/{setup.CourseId}/links/{link}/checklist");
        var write = await SetChecklistAsync(setup, outsider, link, 0, true);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Checklist_ForAnUnknownPart_ReturnsNotFound()
    {
        var setup = await AddCourseWithGroupAsync();

        var response = await SetChecklistAsync(setup, setup.StudentIds[0], Guid.NewGuid(), 0, true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<ChecklistResponse> GetChecklistAsync(Setup setup, Guid studentId, Guid linkId)
    {
        using var student = StudentClient(studentId);
        return await student.GetJsonAsync<ChecklistResponse>(
            $"/api/courses/{setup.CourseId}/links/{linkId}/checklist");
    }

    private async Task<HttpResponseMessage> SetChecklistAsync(
        Setup setup, Guid studentId, Guid linkId, int index, bool isChecked)
    {
        using var student = StudentClient(studentId);
        return await student.PutAsJsonAsync(
            $"/api/courses/{setup.CourseId}/links/{linkId}/checklist/{index}",
            new { @checked = isChecked });
    }

    private async Task<List<ModuleWithContent>> GetStudentModulesAsync(Setup setup, Guid studentId)
    {
        using var student = StudentClient(studentId);
        return await student.GetJsonAsync<List<ModuleWithContent>>(
            $"/api/courses/{setup.CourseId}/student-modules");
    }

    private async Task<HttpResponseMessage> SetCompletionAsync(
        Setup setup, Guid studentId, Guid linkId, bool completed)
    {
        using var student = StudentClient(studentId);
        return await student.PutAsJsonAsync(
            $"/api/courses/{setup.CourseId}/links/{linkId}/completion",
            new { completed });
    }

    private async Task<SubmissionResponse> SubmitTextAsync(
        Setup setup, Guid studentId, Guid linkId, string bodyMd)
    {
        using var student = StudentClient(studentId);
        var form = new MultipartFormDataContent { { new StringContent(bodyMd), "bodyMd" } };
        var response = await student.PostAsync($"/api/links/{linkId}/submissions", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!;
    }

    private HttpClient StudentClient(Guid studentId)
    {
        var student = factory.CreateClient();
        student.DefaultRequestHeaders.Add(TestAuthHandler.Header, studentId.ToString());
        return student;
    }

    private Task<HttpResponseMessage> SaveRoadmapAsync(
        Guid courseId, Guid moduleId, params (Guid LinkId, Guid[] BlockedBy)[] links) =>
        SaveRulesAsync(
            courseId,
            moduleId,
            links.Select(link => (link.LinkId, link.BlockedBy, (string?)null)).ToArray());

    private async Task<HttpResponseMessage> SaveRulesAsync(
        Guid courseId, Guid moduleId, params (Guid LinkId, Guid[] BlockedBy, string? CompletionRule)[] links)
    {
        var body = new
        {
            links = links.Select(link => new
            {
                linkId = link.LinkId,
                blockedByLinkIds = link.BlockedBy,
                completionRule = link.CompletionRule
            })
        };
        var response = await _client.PutAsJsonAsync($"/api/courses/{courseId}/modules/{moduleId}/roadmap", body);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task<Setup> AddCourseWithGroupAsync(int students = 2)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
        var studentIds = new List<Guid>();
        for (var i = 0; i < students; i++)
        {
            studentIds.Add((await TestData.AddMemberAsync(db, group)).Id);
        }

        return new Setup(course.Id, group.Id, studentIds);
    }

    private Task<Guid> LinkTheoryAsync(
        Guid courseId, Guid moduleId, string title, string? bodyMd = null, string? publishAt = null) =>
        LinkContentAsync(courseId, moduleId, new
        {
            type = "theory",
            title,
            bodyMd = bodyMd ?? "Text"
        }, publishAt);

    private Task<Guid> LinkExerciseAsync(Guid courseId, Guid moduleId, string title) =>
        LinkContentAsync(courseId, moduleId, new
        {
            type = "assignment",
            title,
            bodyMd = "",
            submissionModes = new[] { "text" }
        });

    private Task<Guid> LinkAssignmentAsync(Guid courseId, Guid moduleId, string title) =>
        LinkContentAsync(courseId, moduleId, new
        {
            type = "assignment",
            title,
            bodyMd = "",
            gradeScale = "passFail",
            submissionModes = new[] { "text" }
        });

    private async Task<Guid> LinkContentAsync(
        Guid courseId, Guid moduleId, object content, string? publishAt = null)
    {
        var created = await _client.CreateContentAsync(content);
        return (await _client.PostJsonAsync<LinkResponse>(
            $"/api/content/{created.Id}/links", new { courseId, moduleId, publishAt })).Id;
    }

}
