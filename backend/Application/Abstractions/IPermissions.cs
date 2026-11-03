namespace Backend.Application.Abstractions;

/// <summary>
/// Vad den inloggade användaren får göra. Behörigheten prövas här i API:et, klienten får bara reda på
/// rollerna via GET /api/me och kan dölja det användaren inte får göra.
/// Admin bestämmer vem som undervisar vad, läraren bestämmer vad som händer i kursen. Admin har ingen genväg in i
/// undervisningen: lärarrätt kommer bara av en rad i CourseTeachers (eller lärarrollen i en grupp).
/// </summary>
public interface IPermissions
{
    Task<bool> IsAdminAsync(CancellationToken ct);

    /// <summary>Bara den som undervisar i minst en kurs, eller är lärare i en grupp, får skapa innehåll.</summary>
    Task<bool> CanCreateContentAsync(CancellationToken ct);

    /// <summary>Lärare i kursen (en rad i CourseTeachers). Admin räknas inte.</summary>
    Task<bool> CanTeachCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>Lärare i gruppen (GroupRole.Teacher). Admin räknas inte.</summary>
    Task<bool> CanTeachGroupAsync(Guid groupId, CancellationToken ct);

    /// <summary>Lärare i länkens kurs, eller i länkens grupp när länken saknar kurs.</summary>
    Task<bool> CanTeachLinkAsync(Guid linkId, CancellationToken ct);

    /// <summary>Lärare på någon av innehållets länkar.</summary>
    Task<bool> CanTeachContentAsync(Guid contentId, CancellationToken ct);

    /// <summary>
    /// Elev i en grupp som läser en kurs där innehållet ligger i en länk som eleverna ser (publicerat,
    /// PublishAt passerat, veckan öppen), eller i en grupp som innehållet är riktat till.
    /// </summary>
    Task<bool> CanReadPublishedContentAsync(Guid contentId, CancellationToken ct);

    /// <summary>
    /// Admin i kursens skola får ändra kursens titel, datum och poäng och ta bort den. Det ger ingen
    /// undervisningsrätt: beskrivning, veckor, innehåll och betyg hör till kursens lärare.
    /// </summary>
    Task<bool> CanManageCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>Läsa kursens innehåll: lärare eller elev via kursen, eller admin i kursens skola.</summary>
    Task<bool> CanReadCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>Läsa gruppens innehåll: medlem, lärare i en kurs gruppen läser, eller admin i gruppens skola.</summary>
    Task<bool> CanReadGroupAsync(Guid groupId, CancellationToken ct);
}
