using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class ModuleEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record ModuleItem(
        Guid LinkId,
        Guid ContentId,
        string Title,
        string Status,
        string? GradeScale,
        DateTimeOffset PublishAt,
        bool HasStudentActivity,
        int? SubmittedCount = null,
        int? StudentCount = null);

    private record ModuleWithContent(
        Guid Id, string Title, int Position, DateTimeOffset? PublishAt, List<ModuleItem> Content);

    [Fact]
    public async Task CreateModules_AreListedInTheOrderTheyWereCreated()
    {
        var courseId = await AddCourseAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/courses/{courseId}/modules", new { title = " Vecka 1 ", description = " Intro " });
        await _client.CreateModuleAsync(courseId, "Vecka 2");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ModuleResponse>();
        Assert.Equal(("Vecka 1", "Intro", 1), (created!.Title, created.Description, created.Position));
        var modules = await GetModulesAsync(courseId);
        Assert.Equal([("Vecka 1", 1), ("Vecka 2", 2)], modules.Select(module => (module.Title, module.Position)));
    }

    [Fact]
    public async Task CreateModule_WithoutTitle_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();

        var response = await _client.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("title is required", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Modules_ForCourseThatDoesNotExist_Return404()
    {
        var courseId = Guid.NewGuid();

        var list = await _client.GetAsync($"/api/courses/{courseId}/modules");
        var create = await _client.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title = "Vecka 1" });
        var reorder = await _client.PutAsJsonAsync(
            $"/api/courses/{courseId}/modules/order", new { moduleIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reorder.StatusCode);
    }

    [Fact]
    public async Task Reorder_SetsPositionsInTheNewOrder()
    {
        var courseId = await AddCourseAsync();
        var first = await _client.CreateModuleAsync(courseId, "Första");
        var second = await _client.CreateModuleAsync(courseId, "Andra");
        var third = await _client.CreateModuleAsync(courseId, "Tredje");

        var response = await _client.PutAsJsonAsync(
            $"/api/courses/{courseId}/modules/order", new { moduleIds = new[] { third.Id, first.Id, second.Id } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var modules = await GetModulesAsync(courseId);
        Assert.Equal(
            [("Tredje", 1), ("Första", 2), ("Andra", 3)],
            modules.Select(module => (module.Title, module.Position)));
    }

    [Fact]
    public async Task Reorder_WithoutEveryModuleOnce_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var first = await _client.CreateModuleAsync(courseId, "Första");
        var second = await _client.CreateModuleAsync(courseId, "Andra");
        var otherCourseModule = await _client.CreateModuleAsync(await AddCourseAsync(), "Annan kurs");

        Guid[][] invalidOrders =
        [
            [first.Id],
            [first.Id, first.Id],
            [first.Id, otherCourseModule.Id],
            [first.Id, second.Id, otherCourseModule.Id]
        ];
        foreach (var moduleIds in invalidOrders)
        {
            var response = await _client.PutAsJsonAsync($"/api/courses/{courseId}/modules/order", new { moduleIds });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var modules = await GetModulesAsync(courseId);
        Assert.Equal(["Första", "Andra"], modules.Select(module => module.Title));
    }

    [Fact]
    public async Task ContentLinkedToModule_IsListedInTheModule_IncludingDraftsAndScheduled()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var otherWeek = await _client.CreateModuleAsync(courseId, "Vecka 2");
        var later = await CreateContentAsync("Senare", "published");
        var draft = await CreateContentAsync("Utkast", "draft");
        var courseWide = await CreateContentAsync("Hela kursen", "published");

        var link = await LinkAsync(later.Id, new { courseId, moduleId = week.Id, publishAt = "2099-01-01T08:00:00Z" });
        await LinkAsync(draft.Id, new { courseId, moduleId = week.Id, publishAt = "2026-01-01T08:00:00Z" });
        await LinkAsync(courseWide.Id, new { courseId });

        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        var created = await link.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.Equal(week.Id, created!.ModuleId);
        var modules = await GetModulesAsync(courseId);
        var weekContent = modules.Single(module => module.Id == week.Id).Content;
        Assert.Equal(
            [("Senare", "Published"), ("Utkast", "Draft")],
            weekContent.Select(item => (item.Title, item.Status)));
        Assert.Empty(modules.Single(module => module.Id == otherWeek.Id).Content);
    }

    [Fact]
    public async Task ModuleContent_HasTheGradeScaleOfAssignments()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var theory = await CreateContentAsync("Teori", "published");
        var exercise = await _client.PostAsJsonAsync(
            "/api/content", new { type = "assignment", title = "Övning", bodyMd = "" });
        var exerciseId = (await exercise.Content.ReadFromJsonAsync<ContentResponse>())!.Id;

        await LinkAsync(theory.Id, new { courseId, moduleId = week.Id, publishAt = "2026-01-01T08:00:00Z" });
        await LinkAsync(exerciseId, new { courseId, moduleId = week.Id, publishAt = "2026-01-02T08:00:00Z" });

        var content = (await GetModulesAsync(courseId)).Single().Content;
        Assert.Equal([("Teori", null), ("Övning", "None")], content.Select(item => (item.Title, item.GradeScale)));
    }

    [Fact]
    public async Task NewLink_IsLastInTheModule_EvenWhenItPublishesFirst()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var later = await CreateContentAsync("Senare", "published");
        var earlier = await CreateContentAsync("Tidigare", "published");

        await LinkAsync(later.Id, new { courseId, moduleId = week.Id, publishAt = "2026-01-02T08:00:00Z" });
        await LinkAsync(earlier.Id, new { courseId, moduleId = week.Id, publishAt = "2026-01-01T08:00:00Z" });

        var content = (await GetModulesAsync(courseId)).Single().Content;
        Assert.Equal(["Senare", "Tidigare"], content.Select(item => item.Title));
    }

    [Fact]
    public async Task ReorderContent_SetsTheNewOrder()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var first = await CreateContentAsync("Första", "published");
        var second = await CreateContentAsync("Andra", "published");
        var third = await CreateContentAsync("Tredje", "published");
        await LinkAsync(first.Id, new { courseId, moduleId = week.Id, publishAt = "2026-01-01T08:00:00Z" });
        await LinkAsync(second.Id, new { courseId, moduleId = week.Id, publishAt = "2026-01-02T08:00:00Z" });
        await LinkAsync(third.Id, new { courseId, moduleId = week.Id, publishAt = "2026-01-03T08:00:00Z" });
        var linkIds = (await GetModulesAsync(courseId)).Single().Content.Select(item => item.LinkId).ToList();

        var response = await _client.PutAsJsonAsync(
            $"/api/courses/{courseId}/modules/{week.Id}/content/order",
            new { linkIds = new[] { linkIds[2], linkIds[0], linkIds[1] } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var content = (await GetModulesAsync(courseId)).Single().Content;
        Assert.Equal(["Tredje", "Första", "Andra"], content.Select(item => item.Title));
    }

    [Fact]
    public async Task ReorderContent_WithoutEveryLinkOnce_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var otherWeek = await _client.CreateModuleAsync(courseId, "Vecka 2");
        var first = await CreateContentAsync("Första", "published");
        var second = await CreateContentAsync("Andra", "published");
        var other = await CreateContentAsync("Annan vecka", "published");
        await LinkAsync(first.Id, new { courseId, moduleId = week.Id });
        await LinkAsync(second.Id, new { courseId, moduleId = week.Id });
        await LinkAsync(other.Id, new { courseId, moduleId = otherWeek.Id });
        var content = (await GetModulesAsync(courseId)).Single(module => module.Id == week.Id).Content;
        var firstLink = content.Single(item => item.Title == "Första").LinkId;
        var secondLink = content.Single(item => item.Title == "Andra").LinkId;
        var otherLink = (await GetModulesAsync(courseId))
            .Single(module => module.Id == otherWeek.Id).Content.Single().LinkId;

        Guid[][] invalidOrders =
        [
            [firstLink],
            [firstLink, firstLink],
            [firstLink, otherLink],
            [firstLink, secondLink, otherLink]
        ];
        foreach (var linkIds in invalidOrders)
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/courses/{courseId}/modules/{week.Id}/content/order", new { linkIds });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task ReorderContent_ForModuleOfAnotherCourse_ReturnsNotFound()
    {
        var courseId = await AddCourseAsync();
        var otherCourseWeek = await _client.CreateModuleAsync(await AddCourseAsync(), "Vecka 1");
        var content = await CreateContentAsync("Teori", "published");
        await LinkAsync(content.Id, new { courseId, moduleId = otherCourseWeek.Id });

        var response = await _client.PutAsJsonAsync(
            $"/api/courses/{courseId}/modules/{otherCourseWeek.Id}/content/order",
            new { linkIds = Array.Empty<Guid>() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LinkToModule_WithoutCourse_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateContentAsync("Teori", "published");

        var response = await LinkAsync(content.Id, new { moduleId = week.Id, groupId = await AddGroupAsync() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("moduleId requires courseId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LinkToModule_OfAnotherCourse_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var otherCourseWeek = await _client.CreateModuleAsync(await AddCourseAsync(), "Vecka 1");
        var content = await CreateContentAsync("Teori", "published");

        var response = await LinkAsync(content.Id, new { courseId, moduleId = otherCourseWeek.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("module does not belong to the course", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LinkToModule_ThatDoesNotExist_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var content = await CreateContentAsync("Teori", "published");

        var response = await LinkAsync(content.Id, new { courseId, moduleId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("module does not exist", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateModule_WithPublishAt_IsScheduled()
    {
        var courseId = await AddCourseAsync();
        var publishAt = new DateTimeOffset(2027, 1, 5, 8, 0, 0, TimeSpan.Zero);

        var response = await _client.PostAsJsonAsync(
            $"/api/courses/{courseId}/modules",
            new { title = "Vecka 2", publishAt });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ModuleResponse>();
        Assert.Equal(publishAt, created!.PublishAt);
        Assert.Equal(publishAt, (await GetModulesAsync(courseId)).Single().PublishAt);
    }

    [Fact]
    public async Task UpdateModule_RenamesAndChangesPublishAt()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var publishAt = new DateTimeOffset(2027, 2, 1, 8, 0, 0, TimeSpan.Zero);

        var response = await _client.PutAsJsonAsync(
            $"/api/courses/{courseId}/modules/{week.Id}",
            new { title = " Vecka 1 – Intro ", description = " Ny ", publishAt });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ModuleResponse>();
        Assert.Equal(("Vecka 1 – Intro", "Ny", publishAt), (updated!.Title, updated.Description, updated.PublishAt));
    }

    [Fact]
    public async Task DeleteModule_RemovesContentOnlyInWeek_AndKeepsSharedContent()
    {
        var courseId = await AddCourseAsync();
        var firstWeek = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var secondWeek = await _client.CreateModuleAsync(courseId, "Vecka 2");
        var shared = await CreateContentAsync("Delad", "published");
        var onlyFirst = await CreateContentAsync("Bara vecka 1", "published");
        await LinkAsync(shared.Id, new { courseId, moduleId = firstWeek.Id });
        await LinkAsync(shared.Id, new { courseId, moduleId = secondWeek.Id });
        await LinkAsync(onlyFirst.Id, new { courseId, moduleId = firstWeek.Id });

        var response = await _client.DeleteAsync($"/api/courses/{courseId}/modules/{firstWeek.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var modules = await GetModulesAsync(courseId);
        var remaining = Assert.Single(modules);
        Assert.Equal(("Vecka 2", 1), (remaining.Title, remaining.Position));
        Assert.Equal(["Delad"], remaining.Content.Select(item => item.Title));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/content/{shared.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/content/{onlyFirst.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteModule_WithPublishedAssignmentWithoutActivity_IsAllowed()
    {
        var courseId = await AddCourseAsync();
        await AddStudentToCourseAsync(courseId);
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var assignment = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title = "Inlämning",
            bodyMd = "",
            status = "published",
            gradeScale = "passFail",
            submissionModes = new[] { "text" }
        });
        var content = (await assignment.Content.ReadFromJsonAsync<ContentResponse>())!;
        var link = await LinkAsync(content.Id, new { courseId, moduleId = week.Id });
        var linkId = (await link.Content.ReadFromJsonAsync<LinkResponse>())!.Id;
        Assert.Equal(1, await CountSubmissionsAsync(linkId));

        var response = await _client.DeleteAsync($"/api/courses/{courseId}/modules/{week.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetModulesAsync(courseId));
        Assert.Equal(0, await CountSubmissionsAsync(linkId));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/content/{content.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteModule_WithRepoAssignmentWithoutStudentWork_IsAllowed()
    {
        var courseId = await AddCourseAsync();
        await AddStudentToCourseAsync(courseId);
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var assignment = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title = "Labb",
            bodyMd = "",
            status = "published",
            gradeScale = "passFail",
            submissionModes = new[] { "repo" },
            templateRepo = "org/template"
        });
        var content = (await assignment.Content.ReadFromJsonAsync<ContentResponse>())!;
        var link = await LinkAsync(content.Id, new { courseId, moduleId = week.Id });
        var linkId = (await link.Content.ReadFromJsonAsync<LinkResponse>())!.Id;
        Assert.Equal(1, await CountSubmissionsAsync(linkId));

        var response = await _client.DeleteAsync($"/api/courses/{courseId}/modules/{week.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetModulesAsync(courseId));
        Assert.Equal(0, await CountSubmissionsAsync(linkId));
    }

    [Fact]
    public async Task DeleteModule_WithStudentActivity_ReturnsConflict()
    {
        var courseId = await AddCourseAsync();
        var week = await _client.CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateContentAsync("Teori", "published");
        var link = await LinkAsync(content.Id, new { courseId, moduleId = week.Id });
        var linkId = (await link.Content.ReadFromJsonAsync<LinkResponse>())!.Id;

        Guid studentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            studentId = (await TestData.AddUserAsync(db)).Id;
            db.PartCompletions.Add(new PartCompletion
            {
                ContentLinkId = linkId,
                StudentId = studentId
            });
            await db.SaveChangesAsync();
        }

        var listed = (await GetModulesAsync(courseId)).Single().Content.Single();
        Assert.True(listed.HasStudentActivity);

        var response = await _client.DeleteAsync($"/api/courses/{courseId}/modules/{week.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await GetModulesAsync(courseId));
    }

    [Fact]
    public async Task Modules_CountDistinctSubmittersOverStudentsInAllCourseGroups()
    {
        var setup = await AddCourseWithTwoGroupsAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId, "Vecka 1");
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, new { });
        await AddSubmissionsAsync(
            assignment,
            (setup.FirstStudents[0], SubmissionStatus.Graded), // lämnat in, rättad och sedan inlämnad igen
            (setup.SecondStudents[0], SubmissionStatus.NeedsWork),
            (setup.SecondStudents[1], SubmissionStatus.NotStarted)); // utdelad men inte inlämnad

        var item = (await GetModulesAsync(setup.CourseId)).Single().Content.Single();

        Assert.Equal((2, 5), (item.SubmittedCount, item.StudentCount));
    }

    [Fact]
    public async Task Modules_CountOnlyStudentsInTheGroupTheLinkIsFor()
    {
        var setup = await AddCourseWithTwoGroupsAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId, "Vecka 1");
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, new { groupId = setup.SecondGroupId });
        await AddSubmissionsAsync(assignment, (setup.SecondStudents[0], SubmissionStatus.Submitted));

        var item = (await GetModulesAsync(setup.CourseId)).Single().Content.Single();

        Assert.Equal((1, 3), (item.SubmittedCount, item.StudentCount));
    }

    [Fact]
    public async Task Modules_DoNotCountDeactivatedStudents()
    {
        var setup = await AddCourseWithTwoGroupsAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId, "Vecka 1");
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, new { });
        await DeactivateAsync(setup.FirstStudents[0]);
        await AddSubmissionsAsync(
            assignment,
            (setup.FirstStudents[0], SubmissionStatus.Submitted), // avaktiverad, räknas inte
            (setup.FirstStudents[1], SubmissionStatus.Submitted));

        var item = (await GetModulesAsync(setup.CourseId)).Single().Content.Single();

        Assert.Equal((1, 4), (item.SubmittedCount, item.StudentCount));
    }

    private async Task DeactivateAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Users.Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DeactivatedAt, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Modules_HaveNoCountsForTheoryOrExercisesWithoutSubmissionModes()
    {
        var setup = await AddCourseWithTwoGroupsAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId, "Vecka 1");
        var theory = await CreateContentAsync("Teori", "published");
        await LinkAsync(theory.Id, new { courseId = setup.CourseId, moduleId = week.Id });
        await LinkAssignmentAsync(setup.CourseId, week.Id, new { }, submissionModes: []);

        var content = (await GetModulesAsync(setup.CourseId)).Single().Content;

        Assert.Equal(2, content.Count);
        Assert.All(content, item => Assert.Equal((null, null), (item.SubmittedCount, item.StudentCount)));
    }

    [Fact]
    public async Task Modules_HideCountsFromStudents()
    {
        var setup = await AddCourseWithTwoGroupsAsync();
        var week = await _client.CreateModuleAsync(setup.CourseId, "Vecka 1");
        var assignment = await LinkAssignmentAsync(setup.CourseId, week.Id, new { });
        await AddSubmissionsAsync(assignment, (setup.FirstStudents[0], SubmissionStatus.Submitted));
        using var student = factory.CreateClient();
        student.DefaultRequestHeaders.Add(TestAuthHandler.Header, setup.FirstStudents[0].ToString());

        var modules = await student.GetFromJsonAsync<List<ModuleWithContent>>(
            $"/api/courses/{setup.CourseId}/modules");

        var item = modules!.Single().Content.Single();
        Assert.Equal((null, null), (item.SubmittedCount, item.StudentCount));
    }

    private record TwoGroups(
        Guid CourseId, Guid SecondGroupId, List<Guid> FirstStudents, List<Guid> SecondStudents);

    private async Task<TwoGroups> AddCourseWithTwoGroupsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var first = await TestData.AddGroupAsync(db);
        var second = await TestData.AddGroupAsync(db, first.SchoolId);
        var course = await TestData.AddCourseAsync(db, first.SchoolId, first.Id);
        db.GroupCourses.Add(new GroupCourse { GroupId = second.Id, CourseId = course.Id });
        await db.SaveChangesAsync();
        var firstStudents = new List<Guid>();
        var secondStudents = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            firstStudents.Add((await TestData.AddMemberAsync(db, first)).Id);
        }

        for (var i = 0; i < 3; i++)
        {
            secondStudents.Add((await TestData.AddMemberAsync(db, second)).Id);
        }

        // Läraren i första gruppen ska inte räknas som elev.
        await TestData.AddMemberAsync(db, first, GroupRole.Teacher, UserRole.Teacher);
        return new TwoGroups(course.Id, second.Id, firstStudents, secondStudents);
    }

    private async Task<Guid> LinkAssignmentAsync(
        Guid courseId, Guid moduleId, object extra, string[]? submissionModes = null)
    {
        var content = await _client.PostJsonAsync<ContentResponse>("/api/content", new
        {
            type = "assignment",
            title = "Uppgift",
            bodyMd = "",
            status = "published",
            gradeScale = "passFail",
            submissionModes = submissionModes ?? ["file"]
        });
        var body = System.Text.Json.JsonSerializer.SerializeToNode(new { courseId, moduleId })!.AsObject();
        foreach (var property in System.Text.Json.JsonSerializer.SerializeToNode(extra)!.AsObject())
        {
            body[property.Key] = property.Value?.DeepClone();
        }

        var link = await LinkAsync(content.Id, body);
        link.EnsureSuccessStatusCode();
        return (await link.Content.ReadFromJsonAsync<LinkResponse>())!.Id;
    }

    private async Task AddSubmissionsAsync(Guid linkId, params (Guid StudentId, SubmissionStatus Status)[] rows)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Utdelningen skapar redan en rad per elev (NotStarted), och det finns bara en rad per elev och länk.
        var existing = await db.Submissions.Where(submission => submission.ContentLinkId == linkId).ToListAsync();
        foreach (var (studentId, status) in rows)
        {
            var submission = existing.Single(row => row.StudentId == studentId);
            submission.Status = status;
            submission.SubmittedAt = status == SubmissionStatus.NotStarted
                ? null
                : DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private async Task<List<ModuleWithContent>> GetModulesAsync(Guid courseId) =>
        await _client.GetJsonAsync<List<ModuleWithContent>>($"/api/courses/{courseId}/modules");

    private async Task<ContentResponse> CreateContentAsync(string title, string status)
    {
        return await _client.PostJsonAsync<ContentResponse>(
            "/api/content", new { type = "theory", title, bodyMd = "", status });
    }

    private Task<HttpResponseMessage> LinkAsync(Guid contentId, object link) =>
        _client.PostAsJsonAsync($"/api/content/{contentId}/links", link);

    private async Task<Guid> AddCourseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        return (await TestData.AddCourseAsync(db, school.Id)).Id;
    }

    private async Task<Guid> AddGroupAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddGroupAsync(db)).Id;
    }

    private async Task AddStudentToCourseAsync(Guid courseId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var course = await db.Courses.FindAsync(courseId);
        var group = await TestData.AddGroupAsync(db, course!.SchoolId);
        db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = courseId });
        await TestData.AddMemberAsync(db, group);
        await db.SaveChangesAsync();
    }

    private async Task<int> CountSubmissionsAsync(Guid linkId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Submissions.CountAsync(submission => submission.ContentLinkId == linkId);
    }
}
