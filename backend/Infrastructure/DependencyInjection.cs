using Backend.Application.Abstractions;
using Backend.Infrastructure.Auth;
using Backend.Infrastructure.Labs;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Repositories;
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

        services.AddHealthChecks().AddDbContextCheck<AppDb>();

        return services;
    }

    /// <summary>
    /// Postgres: kör migrationer som saknas. SQLite (testerna): skapar tabellerna direkt från modellen,
    /// eftersom migrationerna är skrivna för Postgres.
    /// </summary>
    public static void MigrateDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDb>().Database;

        // EnsureCreated räcker inte mot Supabase: den gör ingenting om databasen redan har tabeller,
        // och Supabase har alltid sina egna (auth, storage). Migrate skapar och uppdaterar våra.
        if (database.IsNpgsql())
        {
            database.Migrate();
        }
        else
        {
            database.EnsureCreated();
        }
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DatabaseOptions>().Bind(config.GetSection(DatabaseOptions.SectionName));

        // Läses vid körning så att t.ex. tester kan byta connection string.
        services.AddDbContext<AppDb>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var provider = databaseOptions.Provider;
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default saknas.");

            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                case "postgres":
                    options.UseNpgsql(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Okänd Database:Provider '{provider}'. Använd 'Sqlite' eller 'Postgres'.");
            }

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
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
    }
}
