using Backend.Infrastructure.Storage;

namespace Backend.Api.Endpoints;

public static class FileEndpoints
{
    // Lämnar ut filer från DiskFileStorage via de signerade länkarna. Signaturen är behörigheten: länken skapas först
    // när en service har kontrollerat att användaren får läsa filen, precis som med Supabases länkar. Utan
    // disklagring (Supabase eller minnet) finns inga sådana länkar, och allt ger 404.
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet($"{DiskFileStorage.RoutePrefix}/{{**key}}", async (
                string key,
                long? expires,
                string? sig,
                IServiceProvider services,
                CancellationToken ct) =>
            {
                var storage = services.GetService<DiskFileStorage>();
                var file = storage is null || expires is null || sig is null
                    ? null
                    : await storage.OpenAsync(key, expires.Value, sig, ct);
                return file is null
                    ? Results.NotFound()
                    : Results.File(file.Value.Path, file.Value.ContentType, enableRangeProcessing: true);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }
}
