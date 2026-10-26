using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Domain.Enums;
using Backend.Infrastructure.Auth;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class ContentEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private InMemoryFileStorage Storage =>
        (InMemoryFileStorage)factory.Services.GetRequiredService<IFileStorage>();

    private record ContentResponse(
        Guid Id, string Type, string Title, Guid AuthorId, string BodyMd, string Status);

    private record ContentDetail(
        Guid Id, string Type, string Title, string BodyMd, string Status, AssignmentDetail? Assignment);

    private record AssignmentDetail(string GradeScale, List<string> SubmissionModes, string? TemplateRepo);

    private record FeedItem(Guid ContentId, string Type, string Title, Guid? GroupId, Guid? CourseId, bool IsPinned);

    private record LinkResponse(Guid Id, Guid ContentId, Guid? CourseId, Guid? ModuleId);

    private record ModuleResponse(Guid Id);

    private record ModuleWithContent(Guid Id, List<ModuleItem> Content);

    private record ModuleItem(
        Guid LinkId,
        Guid ContentId,
        string Title,
        string Status,
        DateTimeOffset PublishAt,
        DateTimeOffset? DueAt,
        bool IsPinned);

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
    public async Task PublishAndUnpublish_ChangesTheFeed()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateTheoryAsync("Publicera", "draft");
        await LinkToGroupAsync(content.Id, groupId);

        var before = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");
        Assert.DoesNotContain(before!, item => item.ContentId == content.Id);

        var published = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/status", new { status = "published" });
        var afterPublish = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Contains(afterPublish!, item => item.ContentId == content.Id);

        await _client.PutAsJsonAsync($"/api/content/{content.Id}/status", new { status = "draft" });
        var afterUnpublish = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");

        Assert.DoesNotContain(afterUnpublish!, item => item.ContentId == content.Id);
    }

    [Fact]
    public async Task UpdateContent_ChangesFields_ButNotTypeOrStatus()
    {
        var content = await CreateTheoryAsync("Gammal", "draft");

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}", new { title = " Ny ", bodyMd = "**text**" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ContentResponse>();
        Assert.Equal(
            ("Theory", "Ny", "**text**", "Draft"),
            (updated!.Type, updated.Title, updated.BodyMd, updated.Status));

        var invalid = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}", new { title = "Teori", bodyMd = "", submissionModes = new[] { "file" } });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task UpdateAssignment_ChangesGradeScaleAndModes()
    {
        var content = await CreateAssignmentAsync("Labb", gradeScale: "passFail", submissionModes: ["file"]);

        var response = await _client.PutAsJsonAsync($"/api/content/{content.Id}", new
        {
            title = "Labb 2",
            bodyMd = "",
            gradeScale = "passFailDistinction",
            submissionModes = new[] { "text" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ContentDetail>();
        Assert.Equal("PassFailDistinction", updated!.Assignment!.GradeScale);
        Assert.Equal(["Text"], updated.Assignment.SubmissionModes);

        var invalid = await _client.PutAsJsonAsync($"/api/content/{content.Id}", new
        {
            title = "Labb 2",
            bodyMd = "",
            submissionModes = new[] { "repo" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task UpdateContentLink_ChangesPublishAtDeadlineAndPin()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateAssignmentAsync("Labb", submissionModes: ["file"]);
        var link = await LinkToModuleAsync(content.Id, courseId, week.Id);
        var publishAt = new DateTimeOffset(2027, 1, 2, 8, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2027, 1, 9, 17, 0, 0, TimeSpan.Zero);

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/links/{link.Id}",
            new { publishAt, dueAt, isPinned = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await GetModulesAsync(courseId)).Single().Content.Single();
        Assert.Equal((publishAt, dueAt, true), (item.PublishAt, item.DueAt, item.IsPinned));
    }

    [Fact]
    public async Task UpdateTheoryLink_WithDueAt_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateTheoryAsync("Teori");
        var link = await LinkToModuleAsync(content.Id, courseId, week.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/links/{link.Id}",
            new { dueAt = DateTimeOffset.UtcNow.AddDays(1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLink_ForAnotherContent_ReturnsNotFound()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var first = await CreateTheoryAsync("Första");
        var second = await CreateTheoryAsync("Andra");
        var link = await LinkToModuleAsync(first.Id, courseId, week.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{second.Id}/links/{link.Id}", new { publishAt = DateTimeOffset.UtcNow });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_WhenLastLink_DeletesContentAndCompactsPositions()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var first = await CreateTheoryAsync("Första");
        var second = await CreateTheoryAsync("Andra");
        var third = await CreateTheoryAsync("Tredje");
        await LinkToModuleAsync(first.Id, courseId, week.Id);
        var middleLink = await LinkToModuleAsync(second.Id, courseId, week.Id);
        await LinkToModuleAsync(third.Id, courseId, week.Id);

        var response = await _client.DeleteAsync($"/api/content/{second.Id}/links/{middleLink.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/content/{second.Id}")).StatusCode);
        var content = (await GetModulesAsync(courseId)).Single().Content;
        Assert.Equal(["Första", "Tredje"], content.Select(item => item.Title));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var positions = await db.ContentLinks
            .Where(link => link.ModuleId == week.Id)
            .OrderBy(link => link.Position)
            .Select(link => link.Position)
            .ToListAsync();
        Assert.Equal([1, 2], positions);
    }

    [Fact]
    public async Task DeleteLink_WhenContentHasAnotherLink_KeepsContent()
    {
        var courseId = await AddCourseAsync();
        var firstWeek = await CreateModuleAsync(courseId, "Vecka 1");
        var secondWeek = await CreateModuleAsync(courseId, "Vecka 2");
        var content = await CreateTheoryAsync("Delad");
        var firstLink = await LinkToModuleAsync(content.Id, courseId, firstWeek.Id);
        await LinkToModuleAsync(content.Id, courseId, secondWeek.Id);

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{firstLink.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/content/{content.Id}")).StatusCode);
        var modules = await GetModulesAsync(courseId);
        Assert.Empty(modules.Single(module => module.Id == firstWeek.Id).Content);
        Assert.Single(modules.Single(module => module.Id == secondWeek.Id).Content);
    }

    [Fact]
    public async Task DeleteLink_WithStudentActivity_ReturnsConflict()
    {
        var (groupId, _) = await AddGroupWithStudentAsync();
        var content = await CreateAssignmentAsync("Övning", submissionModes: ["file"]);
        var link = await LinkToGroupAsync(content.Id, groupId);
        await SetSubmissionStatusAsync(link.Id, SubmissionStatus.Submitted);

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_RemovesNotStartedRowsFromHandout()
    {
        var (groupId, _) = await AddGroupWithStudentAsync();
        var content = await CreateAssignmentAsync("Övning", submissionModes: ["file"]);
        var link = await LinkToGroupAsync(content.Id, groupId);
        Assert.Equal(1, await CountSubmissionsAsync(link.Id));

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountSubmissionsAsync(link.Id));
    }

    [Fact]
    public async Task DeletePublishedGradedAssignment_ReturnsConflict_UntilUnpublished()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateAssignmentAsync("Inlämning", gradeScale: "passFail");
        var link = await LinkToModuleAsync(content.Id, courseId, week.Id);

        var blocked = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");
        await _client.PutAsJsonAsync($"/api/content/{content.Id}/status", new { status = "draft" });
        var deleted = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_WithWrongLink_ReturnsNotFound()
    {
        var content = await CreateTheoryAsync("Teori");

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_LastLink_DeletesAttachmentsFromStorage()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateTheoryAsync("Material med bilaga");
        await UploadAttachmentAsync(content.Id);
        var key = Assert.Single(Storage.Keys, storageKey => storageKey.StartsWith($"content/{content.Id}/"));
        var link = await LinkToGroupAsync(content.Id, groupId);

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(key, Storage.Keys);
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

    private async Task<ContentResponse> CreateTheoryAsync(string title, string status = "published")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/content", new { type = "theory", title, bodyMd = "", status });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }

    private async Task<ContentResponse> CreateAssignmentAsync(
        string title,
        string status = "published",
        string? gradeScale = null,
        string[]? submissionModes = null,
        string? templateRepo = null)
    {
        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title,
            bodyMd = "",
            status,
            gradeScale,
            submissionModes,
            templateRepo
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }

    private async Task<ModuleResponse> CreateModuleAsync(Guid courseId, string title)
    {
        var response = await _client.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ModuleResponse>())!;
    }

    private async Task<LinkResponse> LinkToModuleAsync(Guid contentId, Guid courseId, Guid moduleId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/content/{contentId}/links", new { courseId, moduleId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LinkResponse>())!;
    }

    private async Task<LinkResponse> LinkToGroupAsync(Guid contentId, Guid groupId)
    {
        var response = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", new { groupId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LinkResponse>())!;
    }

    private async Task<List<ModuleWithContent>> GetModulesAsync(Guid courseId) =>
        (await _client.GetFromJsonAsync<List<ModuleWithContent>>($"/api/courses/{courseId}/modules"))!;

    private async Task<Guid> AddCourseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        return (await TestData.AddCourseAsync(db, school.Id)).Id;
    }

    private async Task<(Guid GroupId, Guid StudentId)> AddGroupWithStudentAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db);
        var student = await TestData.AddMemberAsync(db, group);
        return (group.Id, student.Id);
    }

    private async Task SetSubmissionStatusAsync(Guid linkId, SubmissionStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submission = await db.Submissions.SingleAsync(candidate => candidate.ContentLinkId == linkId);
        submission.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<int> CountSubmissionsAsync(Guid linkId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Submissions.CountAsync(submission => submission.ContentLinkId == linkId);
    }

    private async Task UploadAttachmentAsync(Guid contentId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "slides.pdf");
        var response = await _client.PostAsync($"/api/content/{contentId}/attachments", form);
        response.EnsureSuccessStatusCode();
    }

    private record CreatedResponse(Guid Id);

    // Skola, grupp och kurs skapas via API:et, och gruppen får läsa kursen.
    private async Task<(Guid GroupId, Guid CourseId)> AddGroupWithCourseAsync()
    {
        var school = await PostAsync("/api/schools", new { name = "Skola" });
        var group = await PostAsync("/api/groups", new { schoolId = school.Id, type = "class", name = "NET25" });
        var course = await PostAsync("/api/courses", new { schoolId = school.Id, title = "Kurs", code = "KURS" });
        var link = await _client.PostAsync($"/api/groups/{group.Id}/courses/{course.Id}", null);
        link.EnsureSuccessStatusCode();
        return (group.Id, course.Id);
    }

    private async Task<CreatedResponse> PostAsync(string path, object body)
    {
        var response = await _client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>())!;
    }
}
