namespace Backend.Application.Results;

public enum ResultStatus
{
    Ok,
    Invalid, // fel i indata -> 400
    NotFound, // det man pekar på finns inte -> 404
    Conflict, // går inte just nu, t.ex. för att eleven redan har arbetat med delen -> 409
    Forbidden // användaren får inte göra det här -> 403
}

/// <summary>Resultat från en service, så att endpointen kan välja statuskod utan att services känner till HTTP.</summary>
public record ServiceResult<T>(ResultStatus Status, T? Value, string? Error)
{
    /// <summary>Texten i ett 403-svar. Behörigheten prövas av PermissionService, inte av klienten.</summary>
    public const string ForbiddenMessage = "Du har inte behörighet.";

    public static ServiceResult<T> Ok(T value) => new(ResultStatus.Ok, value, null);
    public static ServiceResult<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);
    public static ServiceResult<T> NotFound(string error) => new(ResultStatus.NotFound, default, error);
    public static ServiceResult<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);
    public static ServiceResult<T> Forbidden() => new(ResultStatus.Forbidden, default, ForbiddenMessage);
}
