using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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

        // Elevens Idag-sida: vad som ska göras, det som lämnats in, senaste återkoppling, nyheter och kurserna.
        app.MapGet("/me/today", async (TodayService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(ct))).WithTags("Me");

        // Lärarens Idag-sida: inlämningar att bedöma, deadlines, schemalagda delar och kurserna.
        // Den som inte undervisar i någon kurs får tomma listor.
        app.MapGet("/me/teaching", async (TeachingService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(ct))).WithTags("Me");

        // Adminens Idag-sida: skolans kurser per läge och det som saknar lärare eller lösenord. Bara admin.
        app.MapGet("/me/admin-overview", async (AdminOverviewService service, CancellationToken ct) =>
            (await service.GetAsync(ct)).ToHttp(TypedResults.Ok)).WithTags("Me");

        // Vilka notiser användaren inte vill ha i appen.
        app.MapGet("/me/notification-settings", async Task<Results<Ok<NotificationSettingsDto>, NotFound>> (
            MeService service, CancellationToken ct) =>
        {
            var settings = await service.GetNotificationSettingsAsync(ct);
            return settings is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(settings);
        }).WithTags("Me");

        app.MapPut("/me/notification-settings", async Task<Results<Ok<NotificationSettingsDto>, NotFound>> (
            NotificationSettingsDto body, MeService service, CancellationToken ct) =>
        {
            var settings = await service.SaveNotificationSettingsAsync(body, ct);
            return settings is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(settings);
        }).WithTags("Me");

        // Användarens eget namn, till exempel vid första inloggningen med Microsoft.
        app.MapPut("/me/profile", async (UpdateProfileDto body, MeService service, CancellationToken ct) =>
            (await service.UpdateProfileAsync(body, ct)).ToHttp(TypedResults.Ok)).WithTags("Me");

        // Profilbilden: en bild per anrop, som multipart/form-data under fältet file. Svaret är den nya profilen.
        app.MapPost("/me/avatar", async (
            IFormFile file, AvatarService avatars, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            return (await avatars.SetAsync(new FileUpload(file.FileName, file.Length, stream), ct))
                .ToHttp(TypedResults.Ok);
        })
            .WithTags("Me")
            .DisableAntiforgery() // inloggning med cookies finns inte, så inget CSRF-skydd behövs
            .WithMetadata(new RequestSizeLimitAttribute((int)AvatarPolicy.MaxFileBytes + 64 * 1024));

        app.MapDelete("/me/avatar", async (AvatarService avatars, CancellationToken ct) =>
            (await avatars.RemoveAsync(ct)).ToHttp(TypedResults.Ok))
            .WithTags("Me");

        // Bilden bakom AvatarPolicy.UrlFor. Utan inloggning, eftersom en img-tagg inte skickar med token: adressen
        // innehåller bildens slumpade guid och skickar vidare till en kortlivad länk. Svaret cachas i fem minuter,
        // kortare än länken gäller.
        app.MapGet("/avatars/{userId:guid}/{version}", async Task<Results<RedirectHttpResult, NotFound>> (
            Guid userId, string version, AvatarService avatars, HttpContext http, CancellationToken ct) =>
        {
            var url = await avatars.GetSignedUrlAsync(userId, version, ct);
            if (url is null)
            {
                return TypedResults.NotFound();
            }

            http.Response.Headers.CacheControl = "private, max-age=300";
            return TypedResults.Redirect(url);
        })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }
}
