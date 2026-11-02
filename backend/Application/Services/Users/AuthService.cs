using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Backend.Application.Services;

// Användarnamn eller e-post och lösenord mot vår egen tabell. Microsoft Entra ID är en andra väg in
// (MicrosoftSignInService) som ger samma token.
public class AuthService(
    IUserRepository users,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokens)
{
    public const string DeactivatedMessage = "Ditt konto är spärrat.";

    /// <summary>
    /// Invalid för okänd e-post, en användare utan lösenord och fel lösenord, så att svaret blir detsamma.
    /// Forbidden för en spärrad användare, men bara med rätt lösenord, så att spärren inte avslöjas för andra.
    /// </summary>
    public async Task<ServiceResult<LoginResultDto>> LoginAsync(LoginDto login, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(login.Username, ct);
        if (user?.PasswordHash is null)
        {
            return ServiceResult<LoginResultDto>.Invalid("wrong email or password");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return ServiceResult<LoginResultDto>.Invalid("wrong email or password");
        }

        return user.DeactivatedAt is not null
            ? ServiceResult<LoginResultDto>.Forbidden(DeactivatedMessage)
            : ServiceResult<LoginResultDto>.Ok(new LoginResultDto(tokens.CreateToken(user), tokens.ExpiresAt));
    }
}
