namespace Backend.Application.Abstractions;

/// <summary>Den som loggade in hos Microsoft, från ett ID-token som har kontrollerats.</summary>
public record MicrosoftIdentity(string ObjectId, string TenantId, string Email, string Name);

/// <summary>
/// Lärkas app i skolans Entra ID. Klienten behöver ClientId och TenantId för att logga in med MSAL. AdminObjectId är
/// den som satte upp appen, och SchoolName organisationens namn. Båda används bara när skolan kopplas första gången.
/// </summary>
public record MicrosoftSignInSettings(
    string ClientId,
    string TenantId,
    string? AdminObjectId = null,
    string? SchoolName = null);

/// <summary>Kontrollerar ID-token från Microsoft Entra ID mot Lärkas app (Auth:Entra).</summary>
public interface IMicrosoftTokenValidator
{
    /// <summary>Null när Microsoft-inloggning inte är inställd.</summary>
    MicrosoftSignInSettings? Settings { get; }

    /// <summary>Null om tokenet inte är giltigt: fel signatur, utfärdare, mottagare eller tid.</summary>
    Task<MicrosoftIdentity?> ValidateAsync(string idToken, CancellationToken ct);
}
