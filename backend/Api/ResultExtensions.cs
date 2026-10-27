using Backend.Application.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api;

// Typade svar, så att statuskoderna och svarens typer hamnar i OpenAPI-dokumentet.
// Felmeddelandet skickas som text, utom 403 som är ProblemDetails med en svensk text.
public static class ResultExtensions
{
    /// <summary>Ok -> onOk(värdet), Invalid -> 400, NotFound -> 404, Conflict -> 409, Forbidden -> 403.</summary>
    public static Results<TOk, BadRequest<string>, NotFound<string>, Conflict<string>, ProblemHttpResult> ToHttp<
        T, TOk>(this ServiceResult<T> result, Func<T, TOk> onOk)
        where TOk : IResult
    {
        return result.Status switch
        {
            ResultStatus.Ok => onOk(result.Value!),
            ResultStatus.NotFound => TypedResults.NotFound(result.Error!),
            ResultStatus.Conflict => TypedResults.Conflict(result.Error!),
            ResultStatus.Forbidden => TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden, detail: result.Error),
            _ => TypedResults.BadRequest(result.Error!)
        };
    }

    /// <summary>Ok -> onOk(värdet), Invalid -> 400.</summary>
    public static Results<TOk, BadRequest<string>> ToHttp<T, TOk>(
        this ValidationResult<T> result, Func<T, TOk> onOk)
        where TOk : IResult
    {
        if (result.Error is null)
        {
            return onOk(result.Value!);
        }

        return TypedResults.BadRequest(result.Error);
    }
}
