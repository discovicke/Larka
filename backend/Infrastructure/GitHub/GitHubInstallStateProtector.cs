using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Application.Abstractions;
using Backend.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.GitHub;

/// <summary>
/// Kortlivad, signerad state för GitHub-installationen. Den binder flödet till användaren och skolan
/// utan att vi behöver spara state i databasen.
/// </summary>
public sealed class GitHubInstallStateProtector(
    IOptions<JwtOptions> jwtOptions,
    TimeProvider time) : IGitHubInstallStateProtector
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(15);
    private readonly byte[] _key = Encoding.UTF8.GetBytes(jwtOptions.Value.Key);

    private sealed record Payload(Guid UserId, Guid SchoolId, long ExpiresAt);

    public string Create(Guid userId, Guid schoolId)
    {
        var payload = new Payload(userId, schoolId, time.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds());
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        var encodedPayload = Base64Url(json);
        return $"{encodedPayload}.{Base64Url(Sign(encodedPayload))}";
    }

    public bool TryValidate(string state, Guid userId, out Guid schoolId) =>
        TryValidate(state, out var stateUserId, out schoolId) && stateUserId == userId;

    public bool TryValidate(string state, out Guid userId, out Guid schoolId)
    {
        userId = Guid.Empty;
        schoolId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        var parts = state.Split('.', 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            signature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = Sign(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(signature, expectedSignature))
        {
            return false;
        }

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(payloadBytes);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null
            || payload.ExpiresAt < time.GetUtcNow().ToUnixTimeSeconds())
        {
            return false;
        }

        userId = payload.UserId;
        schoolId = payload.SchoolId;
        return true;
    }

    private byte[] Sign(string encodedPayload)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(Encoding.ASCII.GetBytes(encodedPayload));
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
