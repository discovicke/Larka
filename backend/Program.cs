using System.Reflection;
using System.Text.Json.Serialization;
using Backend.Api;
using Backend.Application;
using Backend.Infrastructure;
using Backend.Infrastructure.Auth;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums skickas som text ("Assignment") i stället för tal, både in och ut.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

    // Det som inte är nullbart måste skickas med, annars 400. OpenAPI-dokumentet läser samma
    // inställningar, så det som står där som obligatoriskt är det också.
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;

    // Tal skickas som tal. Webbstandarden tar även emot "10", och då dokumenteras de som tal eller text.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<EnumSchemaTransformer>();

    // Bearer-knappen i Swagger UI, så att dokumentet kan prövas med tokenet från /api/auth/login.
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// Ogiltig JSON i en request ger 400 med en kort text, både lokalt och i prod (se BadRequestExceptionHandler).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Vid build startar GetDocument.Insider appen för att skriva client/openapi.json. Då finns ingen databas.
var isGeneratingOpenApiDocument = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (!isGeneratingOpenApiDocument)
{
    // Saknad eller för kort JWT-nyckel ska stoppa starten med ett tydligt fel, inte första inloggningen.
    _ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

    app.Services.MigrateDatabase();
    await app.Services.SeedAccountsAsync();
}

app.UseExceptionHandler();

// OpenAPI-dokumentet finns på /openapi/v1.json och Swagger UI på /swagger.
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "RebornLMS API"));

// Användaren läses från token (ClaimsCurrentUser) innan endpoints prövar behörigheten.
app.UseAuthentication();
app.UseAuthorization();

// Klienten byggs in i wwwroot vid dotnet publish. Lokalt körs den via Vite i stället.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapApiEndpoints();
app.MapClient();

app.Run();

// Gör Program synlig för WebApplicationFactory i integrationstesterna.
public partial class Program { }
