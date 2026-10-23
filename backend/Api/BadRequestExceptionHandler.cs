using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace Backend.Api;

/// <summary>
/// Request-body som inte går att läsa (okänt enum-värde, fält som saknas, fel typ) ger 400 med en kort
/// text, samma format som de andra 400-svaren, i stället för en stacktrace.
/// </summary>
public class BadRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        context.Response.StatusCode = badRequest.StatusCode;
        await context.Response.WriteAsJsonAsync(Describe(badRequest), ct);
        return true;
    }

    // JSON-felen innehåller interna typnamn, så det fält som var fel räcker ("invalid value for type").
    private static string Describe(BadHttpRequestException badRequest)
    {
        if (badRequest.InnerException is not JsonException jsonError)
        {
            return badRequest.Message;
        }

        var field = jsonError.Path?.TrimStart('$', '.');
        return string.IsNullOrEmpty(field)
            ? "the request body is not valid JSON for this endpoint, or a required field is missing"
            : $"invalid value for {field}";
    }
}
