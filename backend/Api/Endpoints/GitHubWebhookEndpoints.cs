using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Application.Services;
using Backend.Infrastructure.GitHub;
using Microsoft.Extensions.Options;

namespace Backend.Api.Endpoints;

public static class GitHubWebhookEndpoints
{
    public static IEndpointRouteBuilder MapGitHubWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/github", async (
            HttpContext context,
            SubmissionService submissions,
            IOptions<GitHubOptions> options,
            CancellationToken ct) =>
        {
            var secret = options.Value.WebhookSecret;
            if (string.IsNullOrWhiteSpace(secret))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    detail: "GitHub webhook secret is not configured.");
            }

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            var signature = context.Request.Headers["X-Hub-Signature-256"].ToString();
            if (!IsValidSignature(body, signature, secret))
            {
                return Results.Unauthorized();
            }

            var eventName = context.Request.Headers["X-GitHub-Event"].ToString();
            if (eventName is not ("check_run" or "workflow_run" or "push"))
            {
                return Results.Accepted();
            }

            string? repoFullName;
            try
            {
                using var document = JsonDocument.Parse(body);
                repoFullName = document.RootElement
                    .TryGetProperty("repository", out var repository)
                    && repository.TryGetProperty("full_name", out var fullName)
                    ? fullName.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return Results.BadRequest("Invalid GitHub payload.");
            }

            if (string.IsNullOrWhiteSpace(repoFullName))
            {
                return Results.Accepted();
            }

            await submissions.RefreshRepoByFullNameAsync(repoFullName, ct);
            return Results.Accepted();
        }).AllowAnonymous().WithTags("GitHub webhooks");

        return app;
    }

    private static bool IsValidSignature(string body, string signature, string secret)
    {
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signature["sha256=".Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}
