using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Auth;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class ContentEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record ContentResponse(Guid Id, string Type, string Title, Guid AuthorId);

    private record FeedItem(Guid ContentId, string Type, string Title, Guid? GroupId, Guid? CourseId, bool IsPinned);

    [Fact]
    public async Task CreateContent_ReturnsCreated_WithPlaceholderAuthor()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/content", new { type = "news", title = "Välkommen", bodyMd = "Hej!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ContentResponse>();
        Assert.Equal("News", content!.Type);
        Assert.Equal(PlaceholderCurrentUser.PlaceholderUserId, content.AuthorId);
    }

    [Fact]
    public async Task CreateContent_WithUnknownType_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content", new { type = "meme", title = "x", bodyMd = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LinkToGroup_ShowsInGroupFeed()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("theory", "Loopar");

        var link = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links", new { groupId });

        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        var feed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");
        Assert.Contains(feed!, item => item.ContentId == content.Id && item.Title == "Loopar");
    }

    [Fact]
    public async Task LinkToCourse_ShowsInFeedOfGroupsThatReadIt_Only()
    {
        var (readingGroupId, courseId) = await AddGroupWithCourseAsync();
        var (otherGroupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("theory", "Kursmaterial");

        await _client.PostAsJsonAsync($"/api/content/{content.Id}/links", new { courseId });

        var readingFeed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{readingGroupId}/content");
        var otherFeed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{otherGroupId}/content");
        var courseFeed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/courses/{courseId}/content");
        Assert.Contains(readingFeed!, item => item.ContentId == content.Id);
        Assert.DoesNotContain(otherFeed!, item => item.ContentId == content.Id);
        Assert.Contains(courseFeed!, item => item.ContentId == content.Id);
    }

    [Fact]
    public async Task GroupFeed_FiltersOnType()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var news = await CreateAsync("news", "Nyhet");
        var theory = await CreateAsync("theory", "Teori");
        await _client.PostAsJsonAsync($"/api/content/{news.Id}/links", new { groupId });
        await _client.PostAsJsonAsync($"/api/content/{theory.Id}/links", new { groupId });

        var feed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content?type=news");

        Assert.Equal([news.Id], feed!.Select(item => item.ContentId));
    }

    [Fact]
    public async Task Draft_IsNotInFeed()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var response = await _client.PostAsJsonAsync("/api/content",
            new { type = "news", title = "Utkast", bodyMd = "", status = "draft" });
        var draft = await response.Content.ReadFromJsonAsync<ContentResponse>();
        await _client.PostAsJsonAsync($"/api/content/{draft!.Id}/links", new { groupId });

        var feed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");

        Assert.DoesNotContain(feed!, item => item.ContentId == draft.Id);
    }

    [Fact]
    public async Task Link_WithoutGroupOrCourse_ReturnsBadRequest()
    {
        var content = await CreateAsync("news", "Ingenstans");

        var response = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links", new { isPinned = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_ToGroupThatDoesNotExist_ReturnsBadRequest()
    {
        var content = await CreateAsync("news", "Fel grupp");

        var response = await _client.PostAsJsonAsync(
            $"/api/content/{content.Id}/links", new { groupId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_ToGroupAndCourseItDoesNotRead_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var (_, otherCourseId) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("theory", "Fel kurs");

        var response = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links",
            new { groupId, courseId = otherCourseId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_WithDueAtOnNews_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("news", "Nyhet med deadline");

        var response = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links",
            new { groupId, dueAt = DateTimeOffset.UtcNow.AddDays(7) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SameAssignment_CanHaveDifferentDeadlinesPerGroup()
    {
        var (groupA, _) = await AddGroupWithCourseAsync();
        var (groupB, _) = await AddGroupWithCourseAsync();
        var assignment = await CreateAsync("assignment", "Labb 1");

        var linkA = await _client.PostAsJsonAsync($"/api/content/{assignment.Id}/links",
            new { groupId = groupA, dueAt = DateTimeOffset.UtcNow.AddDays(7) });
        var linkB = await _client.PostAsJsonAsync($"/api/content/{assignment.Id}/links",
            new { groupId = groupB, dueAt = DateTimeOffset.UtcNow.AddDays(14) });

        Assert.Equal(HttpStatusCode.Created, linkA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, linkB.StatusCode);
    }

    [Fact]
    public async Task Comment_OnContent_IsListed()
    {
        var content = await CreateAsync("news", "Kommentera mig");

        var created = await _client.PostAsJsonAsync($"/api/content/{content.Id}/comments", new { bodyMd = "Bra!" });
        var comments = await _client.GetStringAsync($"/api/content/{content.Id}/comments");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("Bra!", comments);
    }

    [Fact]
    public async Task Comment_OnContentThatDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/content/{Guid.NewGuid()}/comments", new { bodyMd = "Hallå?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<ContentResponse> CreateAsync(string type, string title)
    {
        var response = await _client.PostAsJsonAsync("/api/content", new { type, title, bodyMd = "" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }

    // Grupper och kurser kan inte skapas via API:et än (#32), så de läggs in direkt i databasen.
    private async Task<(Guid GroupId, Guid CourseId)> AddGroupWithCourseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db);
        var course = await TestData.AddCourseAsync(db, group.SchoolId, group.Id);
        return (group.Id, course.Id);
    }
}
