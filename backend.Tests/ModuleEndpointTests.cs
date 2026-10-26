using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class ModuleEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record ModuleResponse(Guid Id, Guid CourseId, string Title, string Description, int Position);

    private record ModuleItem(
        Guid LinkId, Guid ContentId, string Title, string Status, string? GradeScale, DateTimeOffset PublishAt);

    private record ModuleWithContent(Guid Id, string Title, int Position, List<ModuleItem> Content);

    private record LinkResponse(Guid Id, Guid? CourseId, Guid? ModuleId);

    private record ContentResponse(Guid Id);

    [Fact]
    public async Task CreateModules_AreListedInTheOrderTheyWereCreated()
    {
        var courseId = await AddCourseAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/courses/{courseId}/modules", new { title = " Vecka 1 ", description = " Intro " });
        await CreateModuleAsync(courseId, "Vecka 2");

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
        var first = await CreateModuleAsync(courseId, "Första");
        var second = await CreateModuleAsync(courseId, "Andra");
        var third = await CreateModuleAsync(courseId, "Tredje");

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
        var first = await CreateModuleAsync(courseId, "Första");
        var second = await CreateModuleAsync(courseId, "Andra");
        var otherCourseModule = await CreateModuleAsync(await AddCourseAsync(), "Annan kurs");

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
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var otherWeek = await CreateModuleAsync(courseId, "Vecka 2");
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
        var week = await CreateModuleAsync(courseId, "Vecka 1");
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
        var week = await CreateModuleAsync(courseId, "Vecka 1");
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
        var week = await CreateModuleAsync(courseId, "Vecka 1");
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
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var otherWeek = await CreateModuleAsync(courseId, "Vecka 2");
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
        var otherCourseWeek = await CreateModuleAsync(await AddCourseAsync(), "Vecka 1");
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
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateContentAsync("Teori", "published");

        var response = await LinkAsync(content.Id, new { moduleId = week.Id, groupId = await AddGroupAsync() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("moduleId requires courseId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LinkToModule_OfAnotherCourse_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var otherCourseWeek = await CreateModuleAsync(await AddCourseAsync(), "Vecka 1");
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

    private async Task<ModuleResponse> CreateModuleAsync(Guid courseId, string title)
    {
        var response = await _client.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ModuleResponse>())!;
    }

    private async Task<List<ModuleWithContent>> GetModulesAsync(Guid courseId) =>
        (await _client.GetFromJsonAsync<List<ModuleWithContent>>($"/api/courses/{courseId}/modules"))!;

    private async Task<ContentResponse> CreateContentAsync(string title, string status)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/content", new { type = "theory", title, bodyMd = "", status });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
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
}
