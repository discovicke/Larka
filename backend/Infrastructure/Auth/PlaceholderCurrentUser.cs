using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Auth;

// Tillfällig tills inloggning via Microsoft Entra ID finns: allt som skapas får samma användar-id.
// Byt registreringen i DependencyInjection mot en implementation som läser användaren från token.
public class PlaceholderCurrentUser : ICurrentUser
{
    public static readonly Guid PlaceholderUserId = Guid.Empty;

    public Guid UserId => PlaceholderUserId;
}
