using Backend.Application.Abstractions;
using Backend.Infrastructure.Labs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.Tests;

/// <summary>
/// Startar appen mot en egen Postgres-databas per testklass så att testerna inte rör dev-databasen.
/// Tabellerna skapas av migrationerna när appen startar, precis som i dev och prod.
/// Standard är testhandlern (X-Test-User), så att övriga tester inte behöver ett token. JwtTestWebApplicationFactory
/// testar riktig JWT.
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

    /// <summary>Av: anropen autentiseras med riktig JWT (eller inte alls), i stället för testhandlern.</summary>
    public bool UseTestAuthentication { get; set; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString.Value);
        builder.UseSetting("Database:SeedDevData", "false");
        builder.UseSetting("Auth:Jwt:Key", "test-only-key-0123456789abcdef0123456789");
        // User-secrets kan peka på sandbox-org:en. Testerna ska vara oberoende av lokal GitHub-konfiguration.
        builder.UseSetting("GitHub:Org", "skol-org");
        builder.UseSetting("GitHub:AppId", "0");
        builder.UseSetting("GitHub:InstallationId", "0");
        builder.UseSetting("GitHub:PrivateKey", "");
        builder.UseSetting("GitHub:PrivateKeyPath", "");
        builder.ConfigureTestServices(services =>
        {
            // Lokala user-secrets kan peka på sandbox-GitHub. Testerna ska alltid använda fake-repon.
            services.RemoveAll<ILabProvisioner>();
            services.AddSingleton<ILabProvisioner, FakeLabProvisioner>();
            services.RemoveAll<IGitHubConnectionStatusProvider>();
            services.AddSingleton<IGitHubConnectionStatusProvider, TestGitHubConnectionStatusProvider>();

            if (!UseTestAuthentication)
            {
                return;
            }

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Tjänster som testas direkt utanför en HTTP-förfrågan har ingen HttpContext. De använder
            // placeholder-admin.
            services.RemoveAll<ICurrentUser>();
            services.AddScoped<ICurrentUser, TestCurrentUser>();
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

/// <summary>Samma testdatabas, men anropen autentiseras med JWT (eller inte alls) som i produktion.</summary>
public sealed class JwtTestWebApplicationFactory : TestWebApplicationFactory
{
    public JwtTestWebApplicationFactory() => UseTestAuthentication = false;
}
