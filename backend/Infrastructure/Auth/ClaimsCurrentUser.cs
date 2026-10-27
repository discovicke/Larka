using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Auth;

// Vem som anropar, från sub-claimen i JWT:n. Här är förlängningspunkten: Microsoft Entra ID (#79) lägger till
// ett eget JWT-schema vars oid mappas till vårt användar-id, och inget annat behöver ändras.
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
