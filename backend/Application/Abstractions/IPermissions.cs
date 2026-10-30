namespace Backend.Application.Abstractions;

/// <summary>
/// Vad den inloggade användaren får göra. Behörigheten prövas här i API:et, klienten får bara reda på
/// rollerna via GET /api/me och kan dölja det användaren inte får göra.
/// </summary>
public interface IPermissions
{
    Task<bool> IsAdminAsync(CancellationToken ct);

    /// <summary>Bara lärare och admin får skapa innehåll.</summary>
    Task<bool> CanCreateContentAsync(CancellationToken ct);

    /// <summary>Admin, eller lärare i kursen.</summary>
    Task<bool> CanTeachCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>Admin, eller lärare i gruppen.</summary>
    Task<bool> CanTeachGroupAsync(Guid groupId, CancellationToken ct);

    /// <summary>Admin, eller lärare i länkens kurs, eller i länkens grupp när länken saknar kurs.</summary>
    Task<bool> CanTeachLinkAsync(Guid linkId, CancellationToken ct);

    /// <summary>Admin, eller lärare på någon av innehållets länkar.</summary>
    Task<bool> CanTeachContentAsync(Guid contentId, CancellationToken ct);
}
