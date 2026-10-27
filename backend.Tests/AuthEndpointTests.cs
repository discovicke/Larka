using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Den riktiga inloggningen: JWT-fabriken har ingen testhandler, så anropen autentiseras som i produktion.
public class AuthEndpointTests(JwtTestWebApplicationFactory factory)
    : IClassFixture<JwtTestWebApplicationFactory>
{
    private const string _password = "rätt-lösenord";

    private readonly HttpClient _client = factory.CreateClient();

    private record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

    private record ProblemResponse(string? Detail);

    private record MeResponse(Guid Id, string Email);

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsATokenThatWorksOnMe()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await AddUserWithPasswordAsync(db, _password);

        // E-post jämförs utan hänsyn till versaler.
        var response = await LoginAsync(user.Email.ToUpperInvariant(), _password);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var me = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal((user.Id, user.Email), (body!.Id, body.Email));
    }

    [Fact]
    public async Task Login_WithUnknownEmailOrNoPasswordOrWrongPassword_ReturnsTheSameUnauthorizedMessage()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await AddUserWithPasswordAsync(db, _password);
        var withoutPassword = await TestData.AddUserAsync(db);

        var unknown = await LoginAsync("finns-inte@example.com", _password);
        var noPassword = await LoginAsync(withoutPassword.Email, _password);
        var wrong = await LoginAsync(user.Email, "fel-lösenord");

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("Fel e-post eller lösenord.", await DetailAsync(unknown));
        Assert.Equal("Fel e-post eller lösenord.", await DetailAsync(noPassword));
        Assert.Equal("Fel e-post eller lösenord.", await DetailAsync(wrong));
    }

    [Fact]
    public async Task MeWithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_StaysAnonymous()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<User> AddUserWithPasswordAsync(AppDb db, string password)
    {
        var user = await TestData.AddUserAsync(db);
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        await db.SaveChangesAsync();
        return user;
    }

    private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { username, password });

    private static async Task<string?> DetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemResponse>())?.Detail;
}
