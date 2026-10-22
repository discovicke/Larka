using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Auth;

// Tillfällig tills inloggning via Microsoft Entra ID finns: allt som skapas får samma användare.
// Användaren (och dess skola) läggs in i databasen av PlaceholderSeed, eftersom AuthorId m.fl. är foreign keys.
// Byt registreringen i DependencyInjection mot en implementation som läser användaren från token.
public class PlaceholderCurrentUser : ICurrentUser
{
    public static readonly Guid PlaceholderSchoolId = new("00000000-0000-0000-0000-00000000000a");
    public static readonly Guid PlaceholderUserId = new("00000000-0000-0000-0000-000000000001");

    public Guid UserId => PlaceholderUserId;
}
