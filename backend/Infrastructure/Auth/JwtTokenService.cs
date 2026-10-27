using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure.Auth;

/// <summary>
/// Vår egen inloggning: e-post och lösenord ger en JWT som API:et sedan läser användaren från.
/// Microsoft Entra ID (#79) lägger till ett eget schema vid sidan av det här i stället för att ändra det.
/// </summary>
public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider time) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public DateTimeOffset ExpiresAt { get; private set; }

    public string CreateToken(User user)
    {
        var now = time.GetUtcNow();
        ExpiresAt = now.AddMinutes(_options.LifetimeMinutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.Name),
                new Claim("role", user.Role.ToString())
            ],
            notBefore: now.UtcDateTime,
            expires: ExpiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
