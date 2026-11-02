using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class RoadmapEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record ModuleResponse(Guid Id);

    private record ContentResponse(Guid Id);

    private record LinkResponse(Guid Id);

    private record ModuleItem(
        Guid LinkId,
        string Title,
        string? CompletionRule,
        List<Guid> BlockedBy,
        string? Status = null);

    private record ModuleWithContent(Guid Id, string Title, List<ModuleItem> Content);

    [Fact]
    public async Task SaveRoadmap_ReturnsTheBlockersInTheModule()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var theoryA = await LinkTheoryAsync(courseId, week.Id, "Teori A");
        var theoryB = await LinkTheoryAsync(courseId, week.Id, "Teori B");
        var exercise = await LinkTheoryAsync(courseId, week.Id, "Övning");

        var response = await SaveAsync(courseId, week.Id,
            (theoryA, []),
            (theoryB, [theoryA]),
            (exercise, [theoryA, theoryB]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.Equal([theoryA], saved.Content.Single(item => item.LinkId == theoryB).BlockedBy);
        Assert.Equal(
            [theoryA, theoryB],
            saved.Content.Single(item => item.LinkId == exercise).BlockedBy);
        var listed = (await GetModulesAsync(courseId)).Single();
        Assert.Equal([theoryA], listed.Content.Single(item => item.LinkId == theoryB).BlockedBy);
    }

    [Fact]
    public async Task SaveRoadmap_OrdersTheWeekByDependencies()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        // Skapas i "fel" ordning: övningen ligger först och teorin som låser upp den sist.
        var exercise = await LinkTheoryAsync(courseId, week.Id, "Övning");
        var theory = await LinkTheoryAsync(courseId, week.Id, "Teori");

        var response = await SaveAsync(courseId, week.Id,
            (exercise, [theory]),
            (theory, []));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.Equal([theory, exercise], saved.Content.Select(item => item.LinkId));
        var listed = (await GetModulesAsync(courseId)).Single();
        Assert.Equal([theory, exercise], listed.Content.Select(item => item.LinkId));
    }

    [Fact]
    public async Task SaveRoadmap_KeepsNewsInTheWeekWithoutDependencies()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var news = await LinkContentAsync(
            courseId, week.Id, new { type = "news", title = "Nyhet", bodyMd = "Text" });
        var theory = await LinkTheoryAsync(courseId, week.Id, "Teori");

        var response = await SaveAsync(courseId, week.Id, (news, []), (theory, []));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.Equal([news, theory], saved.Content.Select(item => item.LinkId));
        Assert.Empty(saved.Content.Single(item => item.LinkId == news).BlockedBy);
    }

    [Fact]
    public async Task SaveRoadmap_ReplacesTheDependenciesOfTheWeek()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var first = await LinkTheoryAsync(courseId, week.Id, "Första");
        var second = await LinkTheoryAsync(courseId, week.Id, "Andra");

        await SaveAsync(courseId, week.Id, (first, []), (second, [first]));
        var response = await SaveAsync(courseId, week.Id, (first, []), (second, []));

        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.Empty(saved.Content.Single(item => item.LinkId == second).BlockedBy);
        Assert.Empty(saved.Content.Single(item => item.LinkId == first).BlockedBy);
    }

    [Fact]
    public async Task SaveRoadmap_WithPartInAnotherWeek_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var otherWeek = await CreateModuleAsync(courseId, "Vecka 2");
        var part = await LinkTheoryAsync(courseId, week.Id, "Vecka 1-del");
        var other = await LinkTheoryAsync(courseId, otherWeek.Id, "Vecka 2-del");

        var response = await SaveAsync(courseId, week.Id, (part, [other]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("same week", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveRoadmap_WithSelfDependency_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var part = await LinkTheoryAsync(courseId, week.Id, "Själv");

        var response = await SaveAsync(courseId, week.Id, (part, [part]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("itself", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveRoadmap_WithDuplicateBlocker_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var first = await LinkTheoryAsync(courseId, week.Id, "Första");
        var second = await LinkTheoryAsync(courseId, week.Id, "Andra");

        var response = await SaveAsync(courseId, week.Id, (first, []), (second, [first, first]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("duplicates", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveRoadmap_WithUnknownLink_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        await LinkTheoryAsync(courseId, week.Id, "Första");

        var response = await SaveAsync(courseId, week.Id, (Guid.NewGuid(), []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("every part in the week", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveRoadmap_WithoutEveryPartOnce_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var first = await LinkTheoryAsync(courseId, week.Id, "Första");
        await LinkTheoryAsync(courseId, week.Id, "Andra");

        var response = await SaveAsync(courseId, week.Id, (first, []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveRoadmap_WithCycle_ReturnsTheMembers()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var first = await LinkTheoryAsync(courseId, week.Id, "Teori A");
        var second = await LinkTheoryAsync(courseId, week.Id, "Övning B");

        var response = await SaveAsync(courseId, week.Id, (first, [second]), (second, [first]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("cycle:", error);
        Assert.Contains("Teori A", error);
        Assert.Contains("Övning B", error);
    }

    [Fact]
    public async Task SaveRoadmap_WithNewsAsBlocker_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var news = await LinkContentAsync(courseId, week.Id, new { type = "news", title = "Nyhet", bodyMd = "Text" });
        var theory = await LinkTheoryAsync(courseId, week.Id, "Teori");

        var response = await SaveAsync(courseId, week.Id, (news, []), (theory, [news]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("news", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveRoadmap_SetsTheCompletionRuleOnAnAssignment()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var assignment = await LinkContentAsync(courseId, week.Id, new
        {
            type = "assignment",
            title = "Inlämning",
            bodyMd = "",
            gradeScale = "passFail",
            submissionModes = new[] { "text" }
        });

        var response = await SaveRulesAsync(courseId, week.Id, (assignment, [], "Approved"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.Equal("Approved", saved.Content.Single().CompletionRule);
    }

    [Fact]
    public async Task SaveRoadmap_PublishesDraftsInTheWeek()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var published = await LinkTheoryAsync(courseId, week.Id, "Publicerad");
        var draft = await LinkContentAsync(courseId, week.Id, new
        {
            type = "theory",
            title = "Utkast",
            bodyMd = "Text",
            status = "draft"
        });

        var response = await SaveAsync(courseId, week.Id, (published, []), (draft, []));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ModuleWithContent>())!;
        Assert.Equal("Published", saved.Content.Single(item => item.LinkId == draft).Status);
    }

    [Fact]
    public async Task SaveRoadmap_WithApprovedOnAnExercise_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var exercise = await LinkContentAsync(courseId, week.Id, new
        {
            type = "assignment",
            title = "Övning",
            bodyMd = "",
            submissionModes = new[] { "text" }
        });

        var response = await SaveRulesAsync(courseId, week.Id, (exercise, [], "Approved"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("grade scale", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveRoadmap_WithCompletionRuleOnTheory_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var theory = await LinkTheoryAsync(courseId, week.Id, "Teori");

        var response = await SaveRulesAsync(courseId, week.Id, (theory, [], "Submitted"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("submission modes", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChangingTheGradeScaleToNone_ResetsAnApprovedRule()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId);
        var assignment = await LinkContentAsync(courseId, week.Id, new
        {
            type = "assignment",
            title = "Inlämning",
            bodyMd = "",
            gradeScale = "passFail",
            submissionModes = new[] { "text" }
        });
        await SaveRulesAsync(courseId, week.Id, (assignment, [], "Approved"));
        var contentId = await ContentIdOfLinkAsync(assignment);

        var updated = await _client.PutAsJsonAsync($"/api/content/{contentId}", new
        {
            title = "Inlämning",
            bodyMd = "",
            gradeScale = "None",
            submissionModes = new[] { "text" }
        });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var module = (await GetModulesAsync(courseId)).Single();
        Assert.Equal("Submitted", module.Content.Single().CompletionRule);
    }

    [Fact]
    public async Task SaveRoadmap_ForModuleOfAnotherCourse_ReturnsNotFound()
    {
        var courseId = await AddCourseAsync();
        var otherWeek = await CreateModuleAsync(await AddCourseAsync());

        var response = await SaveAsync(courseId, otherWeek.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> SaveAsync(
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
        return await _client.PutAsJsonAsync($"/api/courses/{courseId}/modules/{moduleId}/roadmap", body);
    }

    private async Task<List<ModuleWithContent>> GetModulesAsync(Guid courseId) =>
        (await _client.GetFromJsonAsync<List<ModuleWithContent>>($"/api/courses/{courseId}/modules"))!;

    private async Task<Guid> ContentIdOfLinkAsync(Guid linkId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await db.ContentLinks.FindAsync(linkId))!.ContentId;
    }

    private async Task<ModuleResponse> CreateModuleAsync(Guid courseId, string title = "Vecka 1")
    {
        var response = await _client.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ModuleResponse>())!;
    }

    private Task<Guid> LinkTheoryAsync(Guid courseId, Guid moduleId, string title) =>
        LinkContentAsync(courseId, moduleId, new { type = "theory", title, bodyMd = "Text" });

    private async Task<Guid> LinkContentAsync(Guid courseId, Guid moduleId, object content)
    {
        var response = await _client.PostAsJsonAsync("/api/content", content);
        response.EnsureSuccessStatusCode();
        var contentId = (await response.Content.ReadFromJsonAsync<ContentResponse>())!.Id;
        var link = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", new { courseId, moduleId });
        link.EnsureSuccessStatusCode();
        return (await link.Content.ReadFromJsonAsync<LinkResponse>())!.Id;
    }

    private async Task<Guid> AddCourseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        return (await TestData.AddCourseAsync(db, school.Id)).Id;
    }
}
