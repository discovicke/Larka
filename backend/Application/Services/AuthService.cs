using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Backend.Application.Services;

// Användarnamn eller e-post och lösenord mot vår egen tabell. Microsoft Entra ID (#79) blir en andra väg in
// när den finns; den här ändras inte av det.
public class AuthService(
    IUserRepository users,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokens)
{
    /// <summary>Null för okänd e-post, en användare utan lösenord och fel lösenord, så att svaret blir detsamma.</summary>
    public async Task<LoginResultDto?> LoginAsync(LoginDto login, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(login.Username, ct);
        if (user?.PasswordHash is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return new LoginResultDto(tokens.CreateToken(user), tokens.ExpiresAt);
    }
}
