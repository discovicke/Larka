using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        // Deadlines och publiceringstider i intervallet [from, to). Högst 62 dagar per anrop.
        app.MapGet("/calendar", async (
            DateTimeOffset from, DateTimeOffset to, CalendarService service, CancellationToken ct) =>
            (await service.GetAsync(from, to, ct)).ToHttp(TypedResults.Ok)).WithTags("Calendar");

        return app;
    }
}
