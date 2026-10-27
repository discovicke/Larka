using System.Text;

namespace Backend.Infrastructure.Auth;

/// <summary>
/// Auth:Jwt i appsettings. Nyckeln är en hemlighet: user-secrets lokalt och Auth__Jwt__Key på Render,
/// aldrig i appsettings. I Development finns en dev-nyckel i appsettings.Development.json.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    /// <summary>HMAC-SHA256 kräver minst 32 byte.</summary>
    public const int MinimumKeyBytes = 32;

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "rebornlms";
    public string Audience { get; set; } = "rebornlms";
    public int LifetimeMinutes { get; set; } = 480;

    public bool HasValidKey => Encoding.UTF8.GetByteCount(Key) >= MinimumKeyBytes;

    public bool HasValidLifetime => LifetimeMinutes > 0;
}
