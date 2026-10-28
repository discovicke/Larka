using System.Net;
using Backend.Application.Abstractions;

namespace backend.Tests;

public sealed class TestGitHubOAuthService : IGitHubOAuthService
{
    public bool IsConfigured { get; set; } = true;

    public string BuildAuthorizeUrl(string state, string? redirectUri = null) =>
        $"https://github.com/login/oauth/authorize?state={state}";

    public Task<string> ExchangeCodeForLoginAsync(string code, CancellationToken ct)
    {
        if (code == "invalid_code")
        {
            throw new GitHubApiException(HttpStatusCode.BadRequest, "Bad code");
        }

        return Task.FromResult($"github-{code}");
    }
}
