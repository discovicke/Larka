using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Profilbilden: alla användare kan ladda upp, byta och ta bort sin egen. Bilden visas via en adress utan inloggning.
public class AvatarEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private InMemoryFileStorage Storage => (InMemoryFileStorage)factory.Services.GetRequiredService<IFileStorage>();

    private record MeResponse(Guid Id, string Name, string? AvatarUrl);

    private record MemberResponse(Guid UserId, string Name, string? AvatarUrl);

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Admin)]
    public async Task Upload_ForEveryRole_StoresTheImage_AndReturnsItsUrl(UserRole role)
    {
        var userId = await AddUserAsync(role);

        var response = await SendAsync(HttpMethod.Post, "/api/me/avatar", userId, ImageForm("jag.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = (await response.Content.ReadFromJsonAsync<MeResponse>())!;
        Assert.StartsWith($"/api/avatars/{userId}/", me.AvatarUrl);
        Assert.Single(Storage.Keys, key => key.StartsWith($"avatars/{userId}/"));
    }

    [Fact]
    public async Task Upload_AgainReplacesTheOldImage_AndTheOldUrlStopsWorking()
    {
        var userId = await AddUserAsync();
        var first = await UploadAsync(userId, "a.jpg");

        var second = await UploadAsync(userId, "b.webp");

        Assert.NotEqual(first.AvatarUrl, second.AvatarUrl);
        Assert.Single(Storage.Keys, key => key.StartsWith($"avatars/{userId}/"));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(first.AvatarUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await _client.GetAsync(second.AvatarUrl)).StatusCode);
    }

    [Theory]
    [InlineData("cv.pdf")]
    [InlineData("bild.svg")]
    [InlineData("sida.html")]
    public async Task Upload_OfSomethingThatIsNotAnImage_ReturnsBadRequest_AndStoresNothing(string fileName)
    {
        var userId = await AddUserAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/me/avatar", userId, ImageForm(fileName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(Storage.Keys, key => key.StartsWith($"avatars/{userId}/"));
    }

    [Fact]
    public async Task Upload_LargerThanFiveMegabytes_ReturnsBadRequest()
    {
        var userId = await AddUserAsync();

        var response = await SendAsync(
            HttpMethod.Post, "/api/me/avatar", userId, ImageForm("stor.png", new byte[5 * 1024 * 1024 + 1]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Remove_DeletesTheImage_AndTheUrl()
    {
        var userId = await AddUserAsync();
        var uploaded = await UploadAsync(userId, "a.png");

        var response = await SendAsync(HttpMethod.Delete, "/api/me/avatar", userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<MeResponse>())!.AvatarUrl);
        Assert.DoesNotContain(Storage.Keys, key => key.StartsWith($"avatars/{userId}/"));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(uploaded.AvatarUrl)).StatusCode);
    }

    [Fact]
    public async Task Image_IsServedWithoutSignIn_AsARedirectToAShortLivedLink()
    {
        var userId = await AddUserAsync();
        var uploaded = await UploadAsync(userId, "a.png");

        // Ingen inloggningsheader: en img-tagg skickar inte med token.
        var response = await _client.GetAsync(uploaded.AvatarUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://storage.local/avatars/", response.Headers.Location!.ToString());
        Assert.Equal(TimeSpan.FromMinutes(5), response.Headers.CacheControl!.MaxAge);
    }

    [Fact]
    public async Task Image_WithAGuessedVersion_IsNotFound()
    {
        var userId = await AddUserAsync();
        await UploadAsync(userId, "a.png");

        var response = await _client.GetAsync($"/api/avatars/{userId}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GroupMembers_IncludeTheirProfilePicture()
    {
        Guid groupId, adminId, studentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var group = await TestData.AddGroupAsync(db);
            groupId = group.Id;
            adminId = (await TestData.AddUserAsync(db, group.SchoolId, UserRole.Admin)).Id;
            studentId = (await TestData.AddMemberAsync(db, group)).Id;
        }

        var uploaded = await UploadAsync(studentId, "a.png");

        var response = await SendAsync(HttpMethod.Get, $"/api/groups/{groupId}/members", adminId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = Assert.Single((await response.Content.ReadFromJsonAsync<List<MemberResponse>>())!);
        Assert.Equal(uploaded.AvatarUrl, member.AvatarUrl);
    }

    private async Task<Guid> AddUserAsync(UserRole role = UserRole.Student)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddUserAsync(db, role: role)).Id;
    }

    private async Task<MeResponse> UploadAsync(Guid userId, string fileName)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/me/avatar", userId, ImageForm(fileName));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MeResponse>())!;
    }

    private static MultipartFormDataContent ImageForm(string fileName, byte[]? bytes = null)
    {
        var file = new ByteArrayContent(bytes ?? [0x89, 0x50, 0x4E, 0x47]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, Guid userId, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }
}
