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

    public static void EnsureDatabaseCreated(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DatabaseOptions>().Bind(config.GetSection(DatabaseOptions.SectionName));

        // Läses vid körning så att t.ex. tester kan byta connection string.
        services.AddDbContext<AppDb>((sp, o) =>
        {
            var provider = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Provider;
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default saknas.");

            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    o.UseSqlite(connectionString);
                    break;
                case "postgres":
                    o.UseNpgsql(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Okänd Database:Provider '{provider}'. Använd 'Sqlite' eller 'Postgres'.");
            }
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ILabRepository, LabRepository>();
    }
}
