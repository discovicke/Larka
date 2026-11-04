using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Kommentarer på en nyhet i kursen: författaren ändrar och tar bort sin egen, läraren i kursen tar bort vilken
// som helst, admin läser men tar bara bort sina egna. Texten har en övre gräns.
public class CommentEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Scene(Guid ContentId, Guid TeacherId, Guid AdminId, Guid StudentA, Guid StudentB, Guid Outsider);

    private record CommentResponse(Guid Id, Guid AuthorId, string BodyMd, bool CanEdit, bool CanDelete);

    [Fact]
    public async Task Author_CanEditAndDeleteOwnComment()
    {
        var scene = await AddSceneAsync();
        var comment = await CommentAsync(scene, scene.StudentA, "Första");

        var edited = await SendAsync(
            HttpMethod.Put, CommentPath(scene, comment.Id), scene.StudentA, new { bodyMd = "Ändrad" });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("Ändrad", (await edited.Content.ReadFromJsonAsync<CommentResponse>())!.BodyMd);

        var deleted = await SendAsync(HttpMethod.Delete, CommentPath(scene, comment.Id), scene.StudentA);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await ListAsync(scene, scene.StudentA));
    }

    [Fact]
    public async Task OtherStudent_CannotEditOrDelete_AndSeesNoActions()
    {
        var scene = await AddSceneAsync();
        var comment = await CommentAsync(scene, scene.StudentA, "Min");

        var edit = await SendAsync(
            HttpMethod.Put, CommentPath(scene, comment.Id), scene.StudentB, new { bodyMd = "Kapad" });
        var delete = await SendAsync(HttpMethod.Delete, CommentPath(scene, comment.Id), scene.StudentB);

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        var seen = Assert.Single(await ListAsync(scene, scene.StudentB));
        Assert.Equal("Min", seen.BodyMd);
        Assert.False(seen.CanEdit);
        Assert.False(seen.CanDelete);
        var own = Assert.Single(await ListAsync(scene, scene.StudentA));
        Assert.True(own.CanEdit);
        Assert.True(own.CanDelete);
    }

    [Fact]
    public async Task Teacher_CanDeleteAStudentsComment_ButNotEditIt()
    {
        var scene = await AddSceneAsync();
        var comment = await CommentAsync(scene, scene.StudentA, "Olämpligt");

        var listed = Assert.Single(await ListAsync(scene, scene.TeacherId));
        var edit = await SendAsync(
            HttpMethod.Put, CommentPath(scene, comment.Id), scene.TeacherId, new { bodyMd = "Ändrad" });
        var delete = await SendAsync(HttpMethod.Delete, CommentPath(scene, comment.Id), scene.TeacherId);

        Assert.False(listed.CanEdit);
        Assert.True(listed.CanDelete);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await ListAsync(scene, scene.StudentA));
    }

    [Fact]
    public async Task Admin_ReadsComments_ButCannotDeleteOthers()
    {
        var scene = await AddSceneAsync();
        var comment = await CommentAsync(scene, scene.StudentA, "Hej");

        var listed = Assert.Single(await ListAsync(scene, scene.AdminId));
        var delete = await SendAsync(HttpMethod.Delete, CommentPath(scene, comment.Id), scene.AdminId);

        Assert.False(listed.CanDelete);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Outsider_GetsNotFound()
    {
        var scene = await AddSceneAsync();
        var comment = await CommentAsync(scene, scene.StudentA, "Hej");

        var delete = await SendAsync(HttpMethod.Delete, CommentPath(scene, comment.Id), scene.Outsider);
        var missing = await SendAsync(HttpMethod.Delete, CommentPath(scene, Guid.NewGuid()), scene.TeacherId);

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Comment_LongerThanTheLimit_IsRejected()
    {
        var scene = await AddSceneAsync();
        var path = $"/api/content/{scene.ContentId}/comments";

        var tooLong = await SendAsync(HttpMethod.Post, path, scene.StudentA, new { bodyMd = new string('a', 5001) });
        var empty = await SendAsync(HttpMethod.Post, path, scene.StudentA, new { bodyMd = "   " });
        var atLimit = await SendAsync(HttpMethod.Post, path, scene.StudentA, new { bodyMd = new string('a', 5000) });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("a comment can be at most 5000 characters", await tooLong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);

        var comment = (await atLimit.Content.ReadFromJsonAsync<CommentResponse>())!;
        var edit = await SendAsync(
            HttpMethod.Put, CommentPath(scene, comment.Id), scene.StudentA, new { bodyMd = new string('a', 5001) });
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
    }

    private static string CommentPath(Scene scene, Guid commentId) =>
        $"/api/content/{scene.ContentId}/comments/{commentId}";

    private async Task<CommentResponse> CommentAsync(Scene scene, Guid asUser, string body)
    {
        var response = await SendAsync(
            HttpMethod.Post, $"/api/content/{scene.ContentId}/comments", asUser, new { bodyMd = body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
    }

    private async Task<List<CommentResponse>> ListAsync(Scene scene, Guid asUser)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/content/{scene.ContentId}/comments", asUser);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CommentResponse>>())!;
    }

    private async Task<Scene> AddSceneAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id, placeholderTeaches: false);
        var teacher = await TestData.AddCourseTeacherAsync(db, course);
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        var studentA = await TestData.AddMemberAsync(db, group);
        var studentB = await TestData.AddMemberAsync(db, group);
        var outsider = await TestData.AddMemberAsync(db, await TestData.AddGroupAsync(db));

        var news = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.News,
            Title = "Välkomna",
            BodyMd = "Hej klassen",
            Status = ContentStatus.Published
        };
        db.Content.Add(news);
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = news.Id,
            CourseId = course.Id,
            PublishAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        return new Scene(news.Id, teacher.Id, admin.Id, studentA.Id, studentB.Id, outsider.Id);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, Guid asUser, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }
}
