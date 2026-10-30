using Backend.Application.Abstractions;
using Backend.Infrastructure.Labs;
using Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
        builder.UseSetting("Database:SeedDemoData", "false");

        // Jobben körs inte av sig själva i testerna. Testerna anropar tjänsterna direkt.
        builder.UseSetting("Notifications:Job:Enabled", "false");
        builder.UseSetting("GitHub:ProvisioningJob:Enabled", "false");
        builder.UseSetting("GitHub:DeadlineLockJob:Enabled", "false");
        builder.UseSetting("Auth:Jwt:Key", "test-only-key-0123456789abcdef0123456789");
        // User-secrets kan peka på sandbox-org:en. Testerna ska vara oberoende av lokal GitHub-konfiguration.
        // FakeLabProvisioner använder Org för reponamn. InstallationId=0 gör att det inte räknas som legacy.
        builder.UseSetting("GitHub:Org", "skol-org");
        builder.UseSetting("GitHub:AppId", "1234");
        builder.UseSetting("GitHub:AppSlug", "test-app");
        builder.UseSetting("GitHub:InstallationId", "0");
        builder.UseSetting("GitHub:PrivateKey", "test-key");
        builder.UseSetting("GitHub:PrivateKeyPath", "");
        builder.ConfigureTestServices(services =>
        {
            // Lokala user-secrets kan peka på sandbox-GitHub. Testerna ska alltid använda fake-repon.
            services.RemoveAll<ILabProvisioner>();
            services.AddSingleton<ILabProvisioner, FakeLabProvisioner>();
            services.RemoveAll<IGitHubAppService>();
            services.AddSingleton<IGitHubAppService, TestGitHubAppService>();
            services.RemoveAll<IGitHubOAuthService>();
            services.AddSingleton<IGitHubOAuthService, TestGitHubOAuthService>();

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

    // Testernas standardanvändare är placeholder-admin. Admin undervisar inte av sig själv, och bara den som
    // undervisar får skapa innehåll, så placeholder står som lärare i en kurs i en annan skola. Där syns den inte i
    // placeholder-skolans listor. Kurser som testerna skapar får placeholder som lärare via TestData.AddCourseAsync.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        // Demodatans test räknar kurserna i skolan, så där lämnas databasen som seeden gjorde den.
        var seedsDemoData = host.Services.GetRequiredService<IConfiguration>()["Database:SeedDemoData"] == "true";
        if (UseTestAuthentication && !seedsDemoData)
        {
            using var scope = host.Services.CreateScope();
            TestData.EnsurePlaceholderTeachesAsync(scope.ServiceProvider.GetRequiredService<AppDb>())
                .GetAwaiter().GetResult();
        }

        return host;
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
