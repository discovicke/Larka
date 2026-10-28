using System.Net;
using System.Text;
using System.Text.Json;
using Backend.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.GitHub;

public sealed class GitHubOAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<GitHubOptions> options) : IGitHubOAuthService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly GitHubOptions _options = options.Value;

    private sealed record OAuthTokenResponse(
        string? AccessToken,
        string? TokenType,
        string? Error,
        string? ErrorDescription);

    private sealed record GitHubUserResponse(string? Login);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ClientId);

    public string BuildAuthorizeUrl(string state, string? redirectUri = null)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("GitHub:ClientId är inte konfigurerad.");
        }

        var urlBuilder = new StringBuilder("https://github.com/login/oauth/authorize");
        urlBuilder.Append("?client_id=").Append(Uri.EscapeDataString(_options.ClientId));
        urlBuilder.Append("&state=").Append(Uri.EscapeDataString(state));
        urlBuilder.Append("&scope=").Append(Uri.EscapeDataString("read:user"));

        if (!string.IsNullOrWhiteSpace(redirectUri))
        {
            urlBuilder.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        }

        return urlBuilder.ToString();
    }

    public async Task<string> ExchangeCodeForLoginAsync(string code, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(GitHubAppService.HttpClientName);

        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://github.com/login/oauth/access_token");
        tokenRequest.Headers.Accept.ParseAdd("application/json");
        tokenRequest.Headers.UserAgent.ParseAdd(_options.UserAgent);

        var tokenPayload = new
        {
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            code
        };
        var tokenJson = JsonSerializer.Serialize(tokenPayload, _jsonOptions);
        tokenRequest.Content = new StringContent(tokenJson, Encoding.UTF8, "application/json");

        using var tokenResponse = await client.SendAsync(tokenRequest, HttpCompletionOption.ResponseContentRead, ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(ct);
            throw new GitHubApiException(tokenResponse.StatusCode, errorBody);
        }

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>(_jsonOptions, ct);
        if (string.IsNullOrWhiteSpace(tokenData?.AccessToken))
        {
            throw new GitHubApiException(
                HttpStatusCode.BadRequest,
                tokenData?.ErrorDescription ?? tokenData?.Error ?? "Could not get access token from GitHub");
        }

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {tokenData.AccessToken}");
        userRequest.Headers.Accept.ParseAdd("application/vnd.github+json");
        userRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        userRequest.Headers.UserAgent.ParseAdd(_options.UserAgent);

        using var userResponse = await client.SendAsync(userRequest, HttpCompletionOption.ResponseContentRead, ct);
        if (!userResponse.IsSuccessStatusCode)
        {
            var errorBody = await userResponse.Content.ReadAsStringAsync(ct);
            throw new GitHubApiException(userResponse.StatusCode, errorBody);
        }

        var userData = await userResponse.Content.ReadFromJsonAsync<GitHubUserResponse>(_jsonOptions, ct);
        if (string.IsNullOrWhiteSpace(userData?.Login))
        {
            throw new GitHubApiException(HttpStatusCode.BadRequest, "GitHub did not return a user login.");
        }

        return userData.Login;
    }
}
