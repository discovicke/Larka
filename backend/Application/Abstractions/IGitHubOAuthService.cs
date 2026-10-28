namespace Backend.Application.Abstractions;

public interface IGitHubOAuthService
{
    bool IsConfigured { get; }
    string BuildAuthorizeUrl(string state, string? redirectUri = null);
    Task<string> ExchangeCodeForLoginAsync(string code, CancellationToken ct);
}
