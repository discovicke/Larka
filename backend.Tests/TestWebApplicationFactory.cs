using Backend.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

/// <summary>
/// Startar appen mot en egen Postgres-databas per testklass så att testerna inte rör dev-databasen.
/// Tabellerna skapas av migrationerna när appen startar, precis som i dev och prod.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"tests_{Guid.NewGuid():N}";
    private readonly Lazy<string> _connectionString;

    public TestWebApplicationFactory()
    {
        // WithWebHostBuilder kör ConfigureWebHost igen, men samma databas ska återanvändas.
        _connectionString = new Lazy<string>(() => PostgresServer.CreateDatabase(_databaseName));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString.Value);
        builder.UseSetting("Database:SeedDevData", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<ICurrentUser, HeaderCurrentUser>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_connectionString.IsValueCreated)
        {
            PostgresServer.DropDatabase(_databaseName);
        }
    }
}
