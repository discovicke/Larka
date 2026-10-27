using Backend.Application.Abstractions;
using Backend.Infrastructure.Auth;
using Backend.Infrastructure.Labs;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Repositories;
using Backend.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddPersistence(config);

        services.AddSingleton<ICurrentUser, PlaceholderCurrentUser>();

        services.AddSingleton<ILabProvisioner, FakeLabProvisioner>();

        services.AddFileStorage(config);

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
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
    }
}
