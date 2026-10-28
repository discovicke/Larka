using Backend.Application.Dtos;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        // Vem är jag, och vilken roll har jag i varje kurs? Klienten använder det för att dölja
        // det användaren inte får göra. API:et prövar behörigheten själv.
        app.MapGet("/me", async Task<Results<Ok<MeDto>, NotFound>> (
            MeService service, CancellationToken ct) =>
        {
            var me = await service.GetAsync(ct);
            return me is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(me);
        }).WithTags("Me");

        // Elevens studieplan: kurserna hen läser som elev, med poäng, status och betyg.
        // Den som inte är elev i någon grupp får en tom plan.
        app.MapGet("/me/study-plan", async (StudyPlanService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(ct))).WithTags("Me");

        return app;
    }
}
