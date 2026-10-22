using Backend.Application.Results;

namespace Backend.Api;

public static class ResultExtensions
{
    /// <summary>Ok -> onOk(värdet), Invalid -> 400, NotFound -> 404. Felmeddelandet skickas som text.</summary>
    public static IResult ToHttp<T>(this ServiceResult<T> result, Func<T, IResult> onOk) => result.Status switch
    {
        ResultStatus.Ok => onOk(result.Value!),
        ResultStatus.NotFound => Results.NotFound(result.Error),
        _ => Results.BadRequest(result.Error)
    };
}
