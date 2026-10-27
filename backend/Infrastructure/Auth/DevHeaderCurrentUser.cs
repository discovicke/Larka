using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Auth;

// Bara i utveckling: dev-klienten väljer användare med headern X-Dev-User, så rollerna kan prövas innan
// inloggning finns. Utan headern används placeholder-användaren som vanligt. Tas bort av #141.
public class DevHeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string Header = "X-Dev-User";

    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.Request.Headers[Header], out var userId)
            ? userId
            : PlaceholderCurrentUser.PlaceholderUserId;
}
