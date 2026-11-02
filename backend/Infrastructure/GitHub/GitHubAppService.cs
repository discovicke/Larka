using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Application.Abstractions;
using Backend.Domain.Enums;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Backend.Infrastructure.GitHub;

/// <summary>
/// GitHub App REST API. App-JWT:et signeras med appens privata nyckel och byts mot ett installation token.
/// Tokenet cachas per installation och förnyas fem minuter innan det går ut.
/// </summary>
public sealed class GitHubAppService(
    IHttpClientFactory httpClientFactory,
    IOptions<GitHubOptions> options,
    TimeProvider time) : IGitHubAppService
{
    public const string HttpClientName = "github";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly GitHubOptions _options = options.Value;
    private readonly Dictionary<long, CachedToken> _tokens = [];
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(string Token, string ExpiresAt);

    private sealed record InstallationResponse(
        long Id,
        InstallationAccount Account,
        long AppId,
        Dictionary<string, string>? Permissions);

    private sealed record InstallationAccount(string Login, string? Type);

    private sealed record InstallationRepositoriesResponse(List<RepositoryResponse> Repositories);

    private sealed record ContentEntryResponse(string Name, string Path, string Type);

    private sealed record ContentFileResponse(string? Content, string? Encoding);

    private sealed record RepositoryResponse(
        string FullName,
        string HtmlUrl,
        string DefaultBranch,
        bool Private,
        bool IsTemplate,
        bool Archived);

    private sealed record CommitResponse(string Sha, CommitDetail Commit);

    private sealed record CommitDetail(string Message, CommitAuthor? Author);

    private sealed record CommitAuthor(string? Date);

    private sealed record CheckRunsResponse(List<CheckRunResponse>? CheckRuns);

    private sealed record CheckRunResponse(long Id, string Name, string Status, string? Conclusion);

    private sealed record CreateRepositoryRequest(
        string Owner,
        string Name,
        bool Private,
        string? Description);

    private sealed record CollaboratorRequest(string Permission);

    private sealed record ArchiveRepositoryRequest(bool Archived);

    private static readonly HashSet<string> _failureConclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "failure",
        "timed_out",
        "cancelled",
        "action_required",
        "startup_failure",
        "stale"
    };

    public async Task<GitHubInstallationToken> GetInstallationTokenAsync(
        long installationId, CancellationToken ct)
    {
        EnsureInstallationId(installationId);
        EnsureAppCredentials();

        var cached = await GetCachedTokenAsync(installationId, ct);
        return new GitHubInstallationToken(cached.Token, cached.ExpiresAt);
    }

    public async Task<GitHubInstallation> GetInstallationAsync(long installationId, CancellationToken ct)
    {
        EnsureInstallationId(installationId);

        using var response = await SendWithAppJwtAsync(
            HttpMethod.Get,
            $"app/installations/{FormatId(installationId)}",
            null,
            ct);
        var payload = await ReadAsync<InstallationResponse>(response, ct)
            ?? throw new InvalidOperationException("GitHub returned no installation.");
        return new GitHubInstallation(
            payload.Id,
            payload.Account.Login,
            payload.Account.Type ?? "",
            payload.AppId,
            payload.Permissions ?? []);
    }

    public async Task<IReadOnlyList<GitHubRepository>> ListRepositoriesAsync(
        long installationId, CancellationToken ct)
    {
        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Get,
            "installation/repositories?per_page=100",
            token.Token,
            null,
            ct);
        var payload = await ReadAsync<InstallationRepositoriesResponse>(response, ct)
            ?? throw new InvalidOperationException("GitHub returned no repositories.");
        return payload.Repositories.Select(ToRepository).ToList();
    }

    public async Task<IReadOnlyList<GitHubRepository>> ListTemplateRepositoriesAsync(
        long installationId, CancellationToken ct)
    {
        var repositories = await ListRepositoriesAsync(installationId, ct);
        return repositories
            .Where(repository => repository.IsTemplate)
            .OrderBy(repository => repository.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListWorkflowNamesAsync(
        long installationId, string owner, string name, CancellationToken ct)
    {
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Get,
            $"repos/{owner}/{name}/contents/.github/workflows",
            token.Token,
            null,
            ct);
        var entries = await ReadAsync<List<ContentEntryResponse>>(response, ct) ?? [];
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry =>
                     entry.Type == "file"
                     && (entry.Name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                         || entry.Name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))))
        {
            using var fileResponse = await SendAsync(
                HttpMethod.Get,
                $"repos/{owner}/{name}/contents/.github/workflows/{Uri.EscapeDataString(entry.Name)}",
                token.Token,
                null,
                ct);
            var file = await ReadAsync<ContentFileResponse>(fileResponse, ct);
            if (file?.Content is null)
            {
                continue;
            }

            string yaml;
            try
            {
                yaml = Encoding.UTF8.GetString(Convert.FromBase64String(
                    file.Content.Replace("\n", "", StringComparison.Ordinal)));
            }
            catch (FormatException)
            {
                continue;
            }

            names.UnionWith(ParseWorkflowJobNames(yaml));
        }

        return names.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// GitHub Actions check runs namnges efter jobb, inte efter workflow eller steg. Läser därför bara
    /// jobs.*.name (eller job-id när namnet saknas) så att kraven matchar det GitHub faktiskt rapporterar.
    /// </summary>
    internal static IReadOnlyList<string> ParseWorkflowJobNames(string yaml)
    {
        var names = new List<string>();
        var stream = new YamlStream();
        try
        {
            using var reader = new StringReader(yaml);
            stream.Load(reader);
        }
        catch (YamlException)
        {
            // En trasig workflow kan GitHub inte köra. Hoppa över filen i stället för att fälla hela listan.
            return names;
        }

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetChild(root, "jobs", out var jobsNode)
            || jobsNode is not YamlMappingNode jobs)
        {
            return names;
        }

        foreach (var jobEntry in jobs.Children)
        {
            if (jobEntry.Key is not YamlScalarNode jobId || string.IsNullOrWhiteSpace(jobId.Value))
            {
                continue;
            }

            var name = jobId.Value.Trim();
            if (jobEntry.Value is YamlMappingNode job
                && TryGetChild(job, "name", out var nameNode)
                && nameNode is YamlScalarNode nameScalar
                && !string.IsNullOrWhiteSpace(nameScalar.Value))
            {
                name = nameScalar.Value.Trim();
            }

            names.Add(name);
        }

        return names;
    }

    private static bool TryGetChild(YamlMappingNode mapping, string key, out YamlNode value)
    {
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is YamlScalarNode scalar
                && string.Equals(scalar.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public async Task<GitHubRepository> GetRepositoryAsync(
        long installationId, string owner, string name, CancellationToken ct)
    {
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Get,
            $"repos/{owner}/{name}",
            token.Token,
            null,
            ct);
        var payload = await ReadAsync<RepositoryResponse>(response, ct)
            ?? throw new InvalidOperationException("GitHub returned no repository.");
        return ToRepository(payload);
    }

    public async Task<GitHubCommit?> GetLatestCommitAsync(
        long installationId,
        string owner,
        string name,
        CancellationToken ct,
        DateTimeOffset? until = null) =>
        (await GetCommitSummaryAsync(installationId, owner, name, ct, until))?.Latest;

    public async Task<GitHubCommitSummary?> GetCommitSummaryAsync(
        long installationId,
        string owner,
        string name,
        CancellationToken ct,
        DateTimeOffset? until = null)
    {
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));

        var path = $"repos/{owner}/{name}/commits?per_page=1";
        if (until is not null)
        {
            var value = Uri.EscapeDataString(
                until.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            path += $"&until={value}";
        }

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Get,
            path,
            token.Token,
            null,
            ct);
        var payload = await ReadAsync<List<CommitResponse>>(response, ct) ?? [];
        var latest = payload.FirstOrDefault();
        if (latest is null)
        {
            return null;
        }

        DateTimeOffset? authoredAt = null;
        if (DateTimeOffset.TryParse(
            latest.Commit.Author?.Date,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed))
        {
            authoredAt = parsed;
        }

        return new GitHubCommitSummary(
            new GitHubCommit(latest.Sha, latest.Commit.Message, authoredAt),
            CommitCountFromLinkHeader(response) ?? payload.Count);
    }

    public async Task<CheckStatus> GetCheckStatusAsync(
        long installationId, string owner, string name, string reference, CancellationToken ct) =>
        (await GetCheckSummaryAsync(installationId, owner, name, reference, ct)).Status;

    public async Task<GitHubCheckSummary> GetCheckSummaryAsync(
        long installationId, string owner, string name, string reference, CancellationToken ct)
    {
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));
        EnsurePathPart(reference, nameof(reference));

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Get,
            $"repos/{owner}/{name}/commits/{Uri.EscapeDataString(reference)}/check-runs?per_page=100",
            token.Token,
            null,
            ct);
        var payload = await ReadAsync<CheckRunsResponse>(response, ct);
        var checkRuns = payload?.CheckRuns ?? [];
        return new GitHubCheckSummary(
            MapCheckStatus(checkRuns),
            checkRuns.Count(checkRun =>
                string.Equals(checkRun.Conclusion, "success", StringComparison.OrdinalIgnoreCase)),
            checkRuns.Count,
            checkRuns
                .Select(checkRun => new GitHubCheckRun(
                    checkRun.Id,
                    checkRun.Name,
                    checkRun.Status,
                    checkRun.Conclusion))
                .ToList());
    }

    public async Task<GitHubRepository> CreateRepositoryFromTemplateAsync(
        long installationId,
        string templateOwner,
        string templateRepo,
        string owner,
        string name,
        bool isPrivate,
        string? description,
        CancellationToken ct)
    {
        EnsurePathPart(templateOwner, nameof(templateOwner));
        EnsurePathPart(templateRepo, nameof(templateRepo));
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Post,
            $"repos/{templateOwner}/{templateRepo}/generate",
            token.Token,
            new CreateRepositoryRequest(owner, name, isPrivate, description),
            ct);
        var payload = await ReadAsync<RepositoryResponse>(response, ct)
            ?? throw new InvalidOperationException("GitHub returned no repository.");
        return ToRepository(payload);
    }

    public async Task AddCollaboratorAsync(
        long installationId, string owner, string name, string username, CancellationToken ct)
    {
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));
        EnsurePathPart(username, nameof(username));

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Put,
            $"repos/{owner}/{name}/collaborators/{Uri.EscapeDataString(username)}",
            token.Token,
            new CollaboratorRequest("push"),
            ct);
    }

    public async Task RemoveCollaboratorAsync(
        long installationId, string owner, string name, string username, CancellationToken ct)
    {
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));
        EnsurePathPart(username, nameof(username));

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"repos/{owner}/{name}/collaborators/{Uri.EscapeDataString(username)}",
            token.Token,
            null,
            ct);
    }

    public Task ArchiveRepositoryAsync(
        long installationId, string owner, string name, CancellationToken ct) =>
        SetArchivedAsync(installationId, owner, name, true, ct);

    public Task UnarchiveRepositoryAsync(
        long installationId, string owner, string name, CancellationToken ct) =>
        SetArchivedAsync(installationId, owner, name, false, ct);

    private async Task SetArchivedAsync(
        long installationId, string owner, string name, bool archived, CancellationToken ct)
    {
        EnsurePathPart(owner, nameof(owner));
        EnsurePathPart(name, nameof(name));

        var token = await GetInstallationTokenAsync(installationId, ct);
        using var response = await SendAsync(
            HttpMethod.Patch,
            $"repos/{owner}/{name}",
            token.Token,
            new ArchiveRepositoryRequest(archived),
            ct);
    }

    private async Task<CachedToken> GetCachedTokenAsync(long installationId, CancellationToken ct)
    {
        if (TryGetCachedToken(installationId, out var cached))
        {
            return cached;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (TryGetCachedToken(installationId, out cached))
            {
                return cached;
            }

            using var response = await SendWithAppJwtAsync(
                HttpMethod.Post,
                $"app/installations/{FormatId(installationId)}/access_tokens",
                new { },
                ct);
            var payload = await ReadAsync<TokenResponse>(response, ct)
                ?? throw new InvalidOperationException("GitHub returned no installation token.");
            var expiresAt = DateTimeOffset.Parse(
                payload.ExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);
            cached = new CachedToken(payload.Token, expiresAt);
            _tokens[installationId] = cached;
            return cached;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool TryGetCachedToken(long installationId, out CachedToken token)
    {
        if (_tokens.TryGetValue(installationId, out var cached)
            && cached.ExpiresAt > time.GetUtcNow().AddMinutes(5))
        {
            token = cached;
            return true;
        }

        token = null!;
        return false;
    }

    private string CreateAppJwt()
    {
        var now = time.GetUtcNow();
        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddMinutes(-1).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = FormatId(_options.AppId)
        }));
        var unsigned = $"{header}.{payload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(ReadPrivateKeyPem());
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(unsigned),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{unsigned}.{Base64Url(signature)}";
    }

    private string ReadPrivateKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return DecodePrivateKey(_options.PrivateKey);
        }

        if (!string.IsNullOrWhiteSpace(_options.PrivateKeyPath))
        {
            if (!File.Exists(_options.PrivateKeyPath))
            {
                throw new InvalidOperationException(
                    $"GitHub private key file not found: {_options.PrivateKeyPath}");
            }

            return File.ReadAllText(_options.PrivateKeyPath);
        }

        throw new InvalidOperationException("GitHub:PrivateKey or GitHub:PrivateKeyPath is missing.");
    }

    private static string DecodePrivateKey(string value)
    {
        var normalized = value.Replace("\\n", "\n", StringComparison.Ordinal).Trim();
        if (normalized.Contains("-----BEGIN", StringComparison.Ordinal))
        {
            return normalized;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "GitHub:PrivateKey must be a PEM key or a base64-encoded PEM key.",
                exception);
        }
    }

    private async Task<HttpResponseMessage> SendWithAppJwtAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        EnsureAppCredentials();
        return await SendAsync(method, path, CreateAppJwt(), body, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string bearerToken, object? body, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, body.GetType(), _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var statusCode = response.StatusCode;
            response.Dispose();
            throw new GitHubApiException(statusCode, errorBody);
        }

        return response;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);

    private static GitHubRepository ToRepository(RepositoryResponse repository) =>
        new(
            repository.FullName,
            repository.HtmlUrl,
            repository.DefaultBranch,
            repository.Private,
            repository.IsTemplate,
            repository.Archived);

    // GitHub skickar antalet sidor i Link-headern. Med per_page=1 är sista sidan antalet commits.
    private static int? CommitCountFromLinkHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        var last = values
            .SelectMany(value => value.Split(','))
            .FirstOrDefault(value => value.Contains("rel=\"last\"", StringComparison.OrdinalIgnoreCase));
        if (last is null)
        {
            return null;
        }

        var match = Regex.Match(last, @"[?&]page=(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var page)
            ? page
            : null;
    }

    private static CheckStatus MapCheckStatus(IReadOnlyList<CheckRunResponse> checkRuns)
    {
        if (checkRuns.Count == 0)
        {
            return CheckStatus.Pending;
        }

        if (checkRuns.Any(checkRun =>
            !string.Equals(checkRun.Status, "completed", StringComparison.OrdinalIgnoreCase)
            || checkRun.Conclusion is null))
        {
            return CheckStatus.Pending;
        }

        return checkRuns.Any(checkRun => _failureConclusions.Contains(checkRun.Conclusion!))
            ? CheckStatus.Red
            : CheckStatus.Green;
    }

    private static string FormatId(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static void EnsureInstallationId(long installationId)
    {
        if (installationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(installationId), "Installation id must be positive.");
        }
    }

    private static void EnsurePathPart(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }
    }

    private void EnsureAppCredentials()
    {
        if (_options.AppId <= 0)
        {
            throw new InvalidOperationException("GitHub:AppId is missing.");
        }

        if (!_options.HasPrivateKey)
        {
            throw new InvalidOperationException("GitHub:PrivateKey or GitHub:PrivateKeyPath is missing.");
        }
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
