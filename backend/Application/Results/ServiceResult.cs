namespace Backend.Application.Results;

public enum ResultStatus
{
    Ok,
    Invalid, // fel i indata -> 400
    NotFound // det man pekar på finns inte -> 404
}

/// <summary>Resultat från en service, så att endpointen kan välja statuskod utan att services känner till HTTP.</summary>
public record ServiceResult<T>(ResultStatus Status, T? Value, string? Error)
{
    public static ServiceResult<T> Ok(T value) => new(ResultStatus.Ok, value, null);
    public static ServiceResult<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);
    public static ServiceResult<T> NotFound(string error) => new(ResultStatus.NotFound, default, error);
}
