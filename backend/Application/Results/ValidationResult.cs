namespace Backend.Application.Results;

/// <summary>
/// Resultat från en service som bara kan misslyckas på ogiltig indata (400). Kan även det man pekar på
/// saknas (404), använd ServiceResult. Kan bara det saknas, returnera null.
/// </summary>
public record ValidationResult<T>(T? Value, string? Error)
{
    public static ValidationResult<T> Ok(T value) => new(value, null);
    public static ValidationResult<T> Invalid(string error) => new(default, error);
}
