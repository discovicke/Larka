using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Auth;

// Vem som anropar, från sub-claimen i JWT:n. Microsoft-inloggningen ger samma token (MicrosoftSignInService),
// så det här gäller hur användaren än loggade in.
public class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var userId)
                ? userId
                : Guid.Empty;
        }
    }
}
