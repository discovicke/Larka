using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Omnämningar (@) i kommentarer och notisen om nya kommentarer i en tråd. Bara den som får läsa nyheten går att
// nämna och får en notis, och den som skrev får aldrig en om sin egen kommentar.
public class CommentMentionTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Scene(
        Guid ContentId, Guid TeacherId, Guid AdminId, Guid StudentA, Guid StudentB, Guid Outsider, Guid Deactivated);

    private record CommentResponse(Guid Id, string BodyMd);

    private record MentionableResponse(Guid Id, string Name, bool IsTeacher);

    private record NotificationResponse(Guid Id, string Kind, Guid? ContentId, Guid? CommentId, string? ActorName);

    private record NotificationList(List<NotificationResponse> Items, int UnreadCount);

    [Fact]
    public async Task Mentionable_ListsTheReaders_ButNotTheAskerOrOutsiders()
    {
        var scene = await AddSceneAsync();

        var response = await SendAsync(HttpMethod.Get, $"/api/content/{scene.ContentId}/mentionable", scene.StudentA);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listed = (await response.Content.ReadFromJsonAsync<List<MentionableResponse>>())!;
        var ids = listed.Select(user => user.Id).ToHashSet();
        Assert.Contains(scene.TeacherId, ids);
        Assert.Contains(scene.StudentB, ids);
        Assert.DoesNotContain(scene.StudentA, ids);
        Assert.DoesNotContain(scene.Outsider, ids);
        Assert.DoesNotContain(scene.AdminId, ids);
        Assert.DoesNotContain(scene.Deactivated, ids);
        Assert.True(listed.Single(user => user.Id == scene.TeacherId).IsTeacher);
        Assert.False(listed.Single(user => user.Id == scene.StudentB).IsTeacher);
    }

    [Fact]
    public async Task Mentionable_IsNotFound_ForSomeoneWhoCannotRead()
    {
        var scene = await AddSceneAsync();

        var response = await SendAsync(HttpMethod.Get, $"/api/content/{scene.ContentId}/mentionable", scene.Outsider);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Draft_OnlyTheTeachersCanBeMentioned()
    {
        var scene = await AddSceneAsync(status: ContentStatus.Draft);

        var response = await SendAsync(HttpMethod.Get, $"/api/content/{scene.ContentId}/mentionable", scene.TeacherId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<MentionableResponse>>())!);
    }

    [Fact]
    public async Task Mention_NotifiesTheMentionedReader_WithWhoWroteIt()
    {
        var scene = await AddSceneAsync();

        var comment = await CommentAsync(scene, scene.StudentA, $"Hej {Token("Lärare", scene.TeacherId)}, en fråga");

        var mention = Assert.Single(await NotificationsAsync(scene.TeacherId, "CommentMention"));
        Assert.Equal(scene.ContentId, mention.ContentId);
        Assert.Equal(comment.Id, mention.CommentId);
        Assert.False(string.IsNullOrEmpty(mention.ActorName));
        // Den nämnda läraren är också nyhetens författare, men får bara omnämnandet, inte en notis till om tråden.
        Assert.Empty(await NotificationsAsync(scene.TeacherId, "CommentReply"));
    }

    [Fact]
    public async Task Mention_OfOutsiderAdminSelfOrDeactivated_GivesNoNotification()
    {
        var scene = await AddSceneAsync();

        await CommentAsync(scene, scene.StudentA, string.Join(' ',
            Token("Utomstående", scene.Outsider),
            Token("Admin", scene.AdminId),
            Token("Jag", scene.StudentA),
            Token("Spärrad", scene.Deactivated),
            Token("Ingen", Guid.NewGuid())));

        Assert.Empty(await NotificationsAsync(scene.Outsider));
        Assert.Empty(await NotificationsAsync(scene.AdminId));
        Assert.Empty(await NotificationsAsync(scene.StudentA));
        Assert.Empty(await NotificationsAsync(scene.Deactivated));
    }

    [Fact]
    public async Task Edit_OnlyNotifiesPeopleAddedInTheEdit()
    {
        var scene = await AddSceneAsync();
        var comment = await CommentAsync(scene, scene.StudentA, $"Hej {Token("Lärare", scene.TeacherId)}");

        var edited = await SendAsync(HttpMethod.Put, CommentPath(scene, comment.Id), scene.StudentA, new
        {
            bodyMd = $"Hej {Token("Lärare", scene.TeacherId)} och {Token("B", scene.StudentB)}"
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var again = await SendAsync(HttpMethod.Put, CommentPath(scene, comment.Id), scene.StudentA, new
        {
            bodyMd = $"Hej {Token("B", scene.StudentB)}"
        });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var back = await SendAsync(HttpMethod.Put, CommentPath(scene, comment.Id), scene.StudentA, new
        {
            bodyMd = $"Hej {Token("Lärare", scene.TeacherId)} och {Token("B", scene.StudentB)}"
        });
        Assert.Equal(HttpStatusCode.OK, back.StatusCode);

        Assert.Single(await NotificationsAsync(scene.TeacherId, "CommentMention"));
        Assert.Single(await NotificationsAsync(scene.StudentB, "CommentMention"));
    }

    [Fact]
    public async Task NewComment_NotifiesTheAuthorAndEarlierCommenters_OncePerUnreadThread()
    {
        var scene = await AddSceneAsync();

        await CommentAsync(scene, scene.StudentA, "Första");
        Assert.Single(await NotificationsAsync(scene.TeacherId, "CommentReply"));
        Assert.Empty(await NotificationsAsync(scene.StudentA));

        await CommentAsync(scene, scene.StudentB, "Andra");
        await CommentAsync(scene, scene.StudentB, "Tredje");

        // Läraren har redan en oläst om tråden, så den blir inte fler. Elev A får en, elev B ingen om sina egna.
        Assert.Single(await NotificationsAsync(scene.TeacherId, "CommentReply"));
        Assert.Single(await NotificationsAsync(scene.StudentA, "CommentReply"));
        Assert.Empty(await NotificationsAsync(scene.StudentB));

        // När notisen är läst ger nästa kommentar en ny.
        var read = await SendAsync(HttpMethod.Post, "/api/notifications/read-all", scene.TeacherId);
        Assert.True(read.IsSuccessStatusCode);
        await CommentAsync(scene, scene.StudentB, "Fjärde");
        Assert.Equal(2, (await NotificationsAsync(scene.TeacherId, "CommentReply")).Count);
    }

    [Fact]
    public async Task MutedKinds_GetNothing()
    {
        var scene = await AddSceneAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var teacher = await db.Users.SingleAsync(user => user.Id == scene.TeacherId);
            teacher.MutedNotificationKinds = [NotificationKind.CommentMention, NotificationKind.CommentReply];
            await db.SaveChangesAsync();
        }

        await CommentAsync(scene, scene.StudentA, $"Hej {Token("Lärare", scene.TeacherId)}");
        await CommentAsync(scene, scene.StudentB, "Svar");

        Assert.Empty(await NotificationsAsync(scene.TeacherId));
    }

    [Fact]
    public async Task DeletingTheComment_RemovesItsNotifications()
    {
        var scene = await AddSceneAsync();
        var comment = await CommentAsync(scene, scene.StudentA, $"Hej {Token("B", scene.StudentB)}");

        var deleted = await SendAsync(HttpMethod.Delete, CommentPath(scene, comment.Id), scene.StudentA);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await NotificationsAsync(scene.StudentB));
        Assert.Empty(await NotificationsAsync(scene.TeacherId));
    }

    private static string Token(string name, Guid id) => $"@[{name}]({id})";

    private static string CommentPath(Scene scene, Guid commentId) =>
        $"/api/content/{scene.ContentId}/comments/{commentId}";

    private async Task<CommentResponse> CommentAsync(Scene scene, Guid asUser, string body)
    {
        var response = await SendAsync(
            HttpMethod.Post, $"/api/content/{scene.ContentId}/comments", asUser, new { bodyMd = body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
    }

    private async Task<List<NotificationResponse>> NotificationsAsync(Guid asUser, string? kind = null)
    {
        var response = await SendAsync(HttpMethod.Get, "/api/notifications", asUser);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<NotificationList>())!.Items;
        return items.Where(notification => kind is null || notification.Kind == kind).ToList();
    }

    private async Task<Scene> AddSceneAsync(ContentStatus status = ContentStatus.Published)
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
        var deactivated = await TestData.AddMemberAsync(db, group);
        deactivated.DeactivatedAt = DateTimeOffset.UtcNow;
        var outsider = await TestData.AddMemberAsync(db, await TestData.AddGroupAsync(db));

        var news = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.News,
            Title = "Välkomna",
            BodyMd = "Hej klassen",
            Status = status
        };
        db.Content.Add(news);
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = news.Id,
            CourseId = course.Id,
            PublishAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        return new Scene(news.Id, teacher.Id, admin.Id, studentA.Id, studentB.Id, outsider.Id, deactivated.Id);
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
