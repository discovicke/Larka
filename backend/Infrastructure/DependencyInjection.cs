using System.Text;
using Backend.Application.Abstractions;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Infrastructure.Auth;
using Backend.Infrastructure.GitHub;
using Backend.Infrastructure.Labs;
using Backend.Infrastructure.Notifications;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Repositories;
using Backend.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddPersistence(config);
        services.AddAuth(config);

        services.AddGitHub(config);
        services.AddFileStorage(config);

        services.AddOptions<NotificationJobOptions>().Bind(config.GetSection(NotificationJobOptions.SectionName));
        services.AddHostedService<NotificationJob>();

        services.AddHealthChecks().AddDbContextCheck<AppDb>();

        return services;
    }

    /// <summary>
    /// Kör migrationer som saknas. EnsureCreated räcker inte mot Supabase: den gör ingenting om databasen
    /// redan har tabeller, och Supabase har alltid sina egna (auth, storage). Migrate skapar och uppdaterar våra.
    /// </summary>
    public static void MigrateDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDb>().Database.Migrate();
    }

    /// <summary>Lägger in inloggningskontona vid varje start, i alla miljöer. Idempotent.</summary>
    public static async Task SeedAccountsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccountSeed>().SeedAsync(CancellationToken.None);
    }

    /// <summary>
    /// Lägger in demodatan när Database:SeedDemoData är true. Ett fel loggas men stoppar inte starten, eftersom
    /// demodata inte är värd ett driftstopp. Allt sparas i en transaktion, så databasen blir aldrig halvfylld.
    /// </summary>
    public static async Task SeedDemoDataAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        if (!scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.SeedDemoData)
        {
            return;
        }

        try
        {
            await scope.ServiceProvider.GetRequiredService<DemoSeed>().SeedAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            scope.ServiceProvider.GetRequiredService<ILogger<DemoSeed>>()
                .LogError(exception, "Demodatan kunde inte läggas in");
        }
    }

    /// <summary>
    /// Inloggning med användarnamn eller e-post och lösenord, och JWT-token. Allt under /api kräver token, och
    /// användaren läses från token (ClaimsCurrentUser). Microsoft Entra ID (#79) lägger till ett eget JWT-schema
    /// vid sidan av det här, utan att något annat ändras.
    /// </summary>
    private static void AddAuth(this IServiceCollection services, IConfiguration config)
    {
        // Nyckeln kontrolleras när den används (Program), så att appen stoppar tydligt vid start om den saknas.
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .Validate(
                options => options.HasValidKey,
                "Auth:Jwt:Key saknas eller är kortare än 32 byte. Sätt den med user-secrets lokalt och som "
                + "Auth__Jwt__Key på Render.")
            .Validate(options => options.HasValidLifetime, "Auth:Jwt:LifetimeMinutes måste vara större än 0.");

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<AccountSeed>();
        services.AddScoped<DemoSeed>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();

        // Läses först när ett anrop autentiseras, så att ett saknat token ger 401 i stället för fel vid start.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }

    /// <summary>
    /// GitHub App-klienten. Den kräver ingen konfiguration vid start: tjänsten kastar ett tydligt fel först när
    /// något försöker använda den, så tester och lokal utveckling utan GitHub påverkas inte.
    /// </summary>
    private static void AddGitHub(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<GitHubOptions>().Bind(config.GetSection(GitHubOptions.SectionName));
        services.AddHttpClient(GitHubAppService.HttpClientName, (serviceProvider, http) =>
        {
            var github = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(github.ApiBaseUrl)
                ? "https://api.github.com/"
                : github.ApiBaseUrl;
            http.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
        });
        services.AddSingleton<IGitHubAppService, GitHubAppService>();
        services.AddSingleton<IGitHubInstallStateProtector, GitHubInstallStateProtector>();
        services.AddScoped<IGitHubConnectionService, GitHubConnectionService>();
        services.AddScoped<IGitHubOAuthService, GitHubOAuthService>();
        services.AddOptions<GitHubProvisioningOptions>()
            .Bind(config.GetSection(GitHubProvisioningOptions.SectionName));
        services.AddOptions<GitHubDeadlineLockOptions>()
            .Bind(config.GetSection(GitHubDeadlineLockOptions.SectionName));
        services.AddHostedService<GitHubProvisioningJob>();
        services.AddHostedService<GitHubDeadlineLockJob>();

        // Utan app-credentials ska lokal utveckling och tester fortsätta med fake-repon.
        var githubOptions = config.GetSection(GitHubOptions.SectionName).Get<GitHubOptions>();
        if (githubOptions?.IsAppConfigured == true)
        {
            services.AddScoped<ILabProvisioner, GitHubLabProvisioner>();
        }
        else
        {
            services.AddScoped<ILabProvisioner, FakeLabProvisioner>();
        }
    }

    /// <summary>
    /// Supabase Storage när Storage:Supabase är inställt. Annars lagras filerna i minnet i Development, och i
    /// andra miljöer felar första användningen (inte starten, så att bygget av OpenAPI-dokumentet inte påverkas).
    /// </summary>
    private static void AddFileStorage(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(SupabaseStorageOptions.SectionName);
        services.AddOptions<SupabaseStorageOptions>().Bind(section);

        var supabase = section.Get<SupabaseStorageOptions>();
        if (supabase is not null && supabase.IsConfigured)
        {
            services.AddHttpClient<IFileStorage, SupabaseFileStorage>((serviceProvider, http) =>
            {
                var storage = serviceProvider.GetRequiredService<IOptions<SupabaseStorageOptions>>().Value;
                http.BaseAddress = new Uri($"{storage.Url.TrimEnd('/')}/storage/v1/");
                http.DefaultRequestHeaders.Add("apikey", storage.ServiceRoleKey);
                http.DefaultRequestHeaders.Authorization = new("Bearer", storage.ServiceRoleKey);
            });
            return;
        }

        services.AddSingleton<IFileStorage>(serviceProvider =>
            serviceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment()
                ? new InMemoryFileStorage()
                : throw new InvalidOperationException(
                    $"{SupabaseStorageOptions.SectionName}:Url och :ServiceRoleKey saknas."));
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DatabaseOptions>().Bind(config.GetSection(DatabaseOptions.SectionName));

        // Läses vid körning så att t.ex. tester kan byta connection string.
        services.AddDbContext<AppDb>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default saknas.");

            options.UseNpgsql(connectionString);

            // Exempeldata i en tom dev-databas, så att /dev och klienten har något att visa.
            if (databaseOptions.SeedDevData)
            {
                options.UseSeeding((context, _) => DevSeed.Seed((AppDb)context));
                options.UseAsyncSeeding((context, _, cancellationToken) =>
                    DevSeed.SeedAsync((AppDb)context, cancellationToken));
            }
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ISchoolRepository, SchoolRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IModuleRepository, ModuleRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<IPartCompletionRepository, PartCompletionRepository>();
        services.AddScoped<IChecklistRepository, ChecklistRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISchoolGitHubInstallationRepository, SchoolGitHubInstallationRepository>();
    }
}
