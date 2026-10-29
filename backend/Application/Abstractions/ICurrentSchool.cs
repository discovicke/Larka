namespace Backend.Application.Abstractions;

/// <summary>
/// Skolan den inloggade användaren hör till. Skolor, grupper och kurser är avgränsade till den: en annan
/// skolas rader syns inte i listorna och ger 404 när man hämtar dem.
/// </summary>
public interface ICurrentSchool
{
    /// <summary>Null om användaren inte finns, så att inget visas.</summary>
    Task<Guid?> GetIdAsync(CancellationToken ct);

    /// <summary>Raden hör till användarens skola.</summary>
    Task<bool> OwnsAsync(Guid schoolId, CancellationToken ct);
}
