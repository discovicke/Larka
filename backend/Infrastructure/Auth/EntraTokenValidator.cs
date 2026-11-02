using Backend.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure.Auth;

// Kontrollerar ID-tokenet som klienten fick av Microsoft: signerat med tenantens nycklar (hämtas och cachas från
// Microsofts metadata), utfärdat av skolans tenant, till vår app och inte för gammalt. Klienten (MSAL) har redan
// kontrollerat nonce.
public class EntraTokenValidator(
    IOptions<EntraOptions> options,
    IConfigurationManager<OpenIdConnectConfiguration> metadata) : IMicrosoftTokenValidator
{
    private readonly EntraOptions _entra = options.Value;

    public MicrosoftSignInSettings? Settings => _entra.IsConfigured
        ? new MicrosoftSignInSettings(_entra.ClientId, _entra.TenantId)
        : null;

    public async Task<MicrosoftIdentity?> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (!_entra.IsConfigured || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var configuration = await metadata.GetConfigurationAsync(ct);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = _entra.Authority,
            ValidAudience = _entra.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        });
        if (!result.IsValid)
        {
            return null;
        }

        var objectId = Claim(result, "oid");
        var tenantId = Claim(result, "tid");
        // email finns bara när kontot har en e-postadress. preferred_username är inloggningsnamnet, som hos skolor
        // är samma adress.
        var email = Claim(result, "email") ?? Claim(result, "preferred_username");
        if (objectId is null || tenantId != _entra.TenantId || email is null)
        {
            return null;
        }

        return new MicrosoftIdentity(objectId, tenantId, email, Claim(result, "name") ?? email);
    }

    private static string? Claim(TokenValidationResult result, string type) =>
        result.Claims.TryGetValue(type, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
