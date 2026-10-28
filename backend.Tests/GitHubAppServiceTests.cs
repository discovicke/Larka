using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Application.Abstractions;
using Backend.Domain.Enums;
using Backend.Infrastructure.GitHub;
using Microsoft.Extensions.Options;

namespace backend.Tests;

// Testar GitHub-klienten utan nätverk. App-JWT:t signeras med en nyckel som skapas i testet.
public class GitHubAppServiceTests
{
    private const long _appId = 5508268;
    private const long _installationId = 176124626;
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-10-28T10:00:00Z");

    [Fact]
    public async Task GetInstallationToken_CreatesSignedJwtAndCachesToken()
    {
        var calls = 0;
        string? authorization = null;
        var handler = new StubHandler((request, _) =>
        {
            calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                $"/app/installations/{_installationId}/access_tokens",
                request.RequestUri!.AbsolutePath);
            authorization = request.Headers.TryGetValues("Authorization", out var values)
                ? values.Single()
                : null;
            return Task.FromResult(TokenResponse(_now.AddHours(1)));
        });
        var (service, _) = CreateService(handler);

        var first = await service.GetInstallationTokenAsync(_installationId, CancellationToken.None);
        var second = await service.GetInstallationTokenAsync(_installationId, CancellationToken.None);

        Assert.Equal("installation-token", first.Token);
        Assert.Equal("installation-token", second.Token);
        Assert.Equal(1, calls);

        Assert.NotNull(authorization);
        var jwt = authorization!["Bearer ".Length..];
        Assert.Equal(3, jwt.Split('.').Length);

        using var payload = JsonDocument.Parse(Base64UrlDecode(jwt.Split('.')[1]));
        Assert.Equal(
            _appId.ToString(CultureInfo.InvariantCulture),
            payload.RootElement.GetProperty("iss").GetString());
        Assert.True(
            payload.RootElement.GetProperty("exp").GetInt64()
            > payload.RootElement.GetProperty("iat").GetInt64());
    }

    [Fact]
    public async Task InstallationToken_RefreshesWhenLessThanFiveMinutesRemain()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(TokenResponse(_now.AddMinutes(10)));
        });
        var (service, time) = CreateService(handler);

        await service.GetInstallationTokenAsync(_installationId, CancellationToken.None);
        time.UtcNow = _now.AddMinutes(6);
        await service.GetInstallationTokenAsync(_installationId, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ListTemplateRepositories_ReturnsOnlyTemplates()
    {
        var handler = new StubHandler((request, _) =>
        {
            if (IsTokenRequest(request))
            {
                return Task.FromResult(TokenResponse(_now.AddHours(1)));
            }

            Assert.Equal("/installation/repositories", request.RequestUri!.AbsolutePath);
            Assert.Contains("per_page=100", request.RequestUri.Query);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, """
                {
                  "total_count": 2,
                  "repositories": [
                    {
                      "full_name": "EduEdugrade-Sandbox/Template",
                      "html_url": "https://github.com/EduEdugrade-Sandbox/Template",
                      "default_branch": "main",
                      "private": false,
                      "is_template": true,
                      "archived": false
                    },
                    {
                      "full_name": "EduEdugrade-Sandbox/Other",
                      "html_url": "https://github.com/EduEdugrade-Sandbox/Other",
                      "default_branch": "main",
                      "private": true,
                      "is_template": false,
                      "archived": false
                    }
                  ]
                }
                """));
        });
        var (service, _) = CreateService(handler);

        var templates = await service.ListTemplateRepositoriesAsync(
            _installationId, CancellationToken.None);

        var template = Assert.Single(templates);
        Assert.Equal("EduEdugrade-Sandbox/Template", template.FullName);
        Assert.True(template.IsTemplate);
        Assert.False(template.IsPrivate);
    }

    [Fact]
    public async Task CreateRepositoryFromTemplate_SendsOwnerNameAndPrivacy()
    {
        string? body = null;
        var handler = new StubHandler(async (request, ct) =>
        {
            if (IsTokenRequest(request))
            {
                return TokenResponse(_now.AddHours(1));
            }

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "/repos/EduEdugrade-Sandbox/Template/generate",
                request.RequestUri!.AbsolutePath);
            body = await request.Content!.ReadAsStringAsync(ct);
            return JsonResponse(HttpStatusCode.Created, """
                {
                  "full_name": "EduEdugrade-Sandbox/lab-1-alice",
                  "html_url": "https://github.com/EduEdugrade-Sandbox/lab-1-alice",
                  "default_branch": "main",
                  "private": true,
                  "is_template": false,
                  "archived": false
                }
                """);
        });
        var (service, _) = CreateService(handler);

        var repository = await service.CreateRepositoryFromTemplateAsync(
            _installationId,
            "EduEdugrade-Sandbox",
            "Template",
            "EduEdugrade-Sandbox",
            "lab-1-alice",
            true,
            "Labb 1",
            CancellationToken.None);

        Assert.Equal("EduEdugrade-Sandbox/lab-1-alice", repository.FullName);
        Assert.True(repository.IsPrivate);

        using var document = JsonDocument.Parse(body!);
        Assert.Equal("EduEdugrade-Sandbox", document.RootElement.GetProperty("owner").GetString());
        Assert.Equal("lab-1-alice", document.RootElement.GetProperty("name").GetString());
        Assert.True(document.RootElement.GetProperty("private").GetBoolean());
    }

    [Fact]
    public async Task AddCollaborator_SendsPushPermission()
    {
        string? body = null;
        var handler = new StubHandler(async (request, ct) =>
        {
            if (IsTokenRequest(request))
            {
                return TokenResponse(_now.AddHours(1));
            }

            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(
                "/repos/EduEdugrade-Sandbox/lab-1-alice/collaborators/alice",
                request.RequestUri!.AbsolutePath);
            body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var (service, _) = CreateService(handler);

        await service.AddCollaboratorAsync(
            _installationId, "EduEdugrade-Sandbox", "lab-1-alice", "alice", CancellationToken.None);

        using var document = JsonDocument.Parse(body!);
        Assert.Equal("push", document.RootElement.GetProperty("permission").GetString());
    }

    [Fact]
    public async Task GetLatestCommit_ParsesShaMessageAndDate()
    {
        var handler = new StubHandler((request, _) =>
        {
            if (IsTokenRequest(request))
            {
                return Task.FromResult(TokenResponse(_now.AddHours(1)));
            }

            Assert.Equal("/repos/EduEdugrade-Sandbox/lab-1-alice/commits", request.RequestUri!.AbsolutePath);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, """
                [
                  {
                    "sha": "abc123",
                    "commit": {
                      "message": "Fix pipeline\n\nDetails",
                      "author": { "date": "2026-10-28T09:30:00Z" }
                    }
                  }
                ]
                """));
        });
        var (service, _) = CreateService(handler);

        var commit = await service.GetLatestCommitAsync(
            _installationId, "EduEdugrade-Sandbox", "lab-1-alice", CancellationToken.None);

        Assert.NotNull(commit);
        Assert.Equal("abc123", commit!.Sha);
        Assert.Equal("Fix pipeline\n\nDetails", commit.Message);
        Assert.Equal(DateTimeOffset.Parse("2026-10-28T09:30:00Z"), commit.AuthoredAt);
    }

    [Theory]
    [InlineData("""{"check_runs":[]}""", CheckStatus.Pending)]
    [InlineData(
        """{"check_runs":[{"status":"completed","conclusion":"success"}]}""",
        CheckStatus.Green)]
    [InlineData(
        """{"check_runs":[{"status":"completed","conclusion":"failure"}]}""",
        CheckStatus.Red)]
    [InlineData(
        """{"check_runs":[{"status":"in_progress","conclusion":null}]}""",
        CheckStatus.Pending)]
    public async Task GetCheckStatus_MapsCheckRuns(string json, CheckStatus expected)
    {
        var handler = new StubHandler((request, _) =>
        {
            if (IsTokenRequest(request))
            {
                return Task.FromResult(TokenResponse(_now.AddHours(1)));
            }

            Assert.Equal(
                "/repos/EduEdugrade-Sandbox/lab-1-alice/commits/abc123/check-runs",
                request.RequestUri!.AbsolutePath);
            Assert.Contains("per_page=100", request.RequestUri.Query);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, json));
        });
        var (service, _) = CreateService(handler);

        var status = await service.GetCheckStatusAsync(
            _installationId, "EduEdugrade-Sandbox", "lab-1-alice", "abc123", CancellationToken.None);

        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task ApiError_ThrowsWithStatusAndBody()
    {
        var handler = new StubHandler((request, _) =>
        {
            if (IsTokenRequest(request))
            {
                return Task.FromResult(TokenResponse(_now.AddHours(1)));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));
        });
        var (service, _) = CreateService(handler);

        var exception = await Assert.ThrowsAsync<GitHubApiException>(() =>
            service.GetRepositoryAsync(
                _installationId, "EduEdugrade-Sandbox", "missing", CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains("Not Found", exception.ResponseBody);
    }

    private static (GitHubAppService Service, TestTimeProvider Time) CreateService(
        HttpMessageHandler handler, string? privateKey = null)
    {
        var time = new TestTimeProvider(_now);
        var options = Options.Create(new GitHubOptions
        {
            AppId = _appId,
            InstallationId = _installationId,
            Org = "EduEdugrade-Sandbox",
            PrivateKey = privateKey ?? CreatePrivateKeyPem(),
            ApiBaseUrl = "https://api.github.test/"
        });
        return (new GitHubAppService(new StubHttpClientFactory(handler), options, time), time);
    }

    private static string CreatePrivateKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private static bool IsTokenRequest(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/access_tokens", StringComparison.Ordinal);

    private static HttpResponseMessage TokenResponse(DateTimeOffset expiresAt) =>
        JsonResponse(
            HttpStatusCode.Created,
            $$"""{"token":"installation-token","expires_at":"{{expiresAt:O}}"}""");

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        var padding = (4 - base64.Length % 4) % 4;
        return Convert.FromBase64String(base64.PadRight(base64.Length + padding, '='));
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.github.test/")
            };
    }
}
