using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Auth;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Backend.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests;

// Auth:FirstAdmin ger en skola utan Microsoft-inloggning sin första admin. Testdatabasen har demokontot admin, så
// testerna spärrar först alla admin med lösenord, som i en ny installation.
public class FirstAdminSeedTests(JwtTestWebApplicationFactory factory) : IClassFixture<JwtTestWebApplicationFactory>
{
    private const string _password = "ett-langt-losenord";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Seed_WithoutAnAdminThatCanSignIn_CreatesOne_WhoCanSignIn()
    {
        await DeactivateAdminsAsync();
        var email = $"rektor.{Guid.NewGuid():N}@skolan.se";

        await SeedAsync(new FirstAdminOptions { Email = email, Password = _password });

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { username = email, password = _password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var admin = await db.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(PlaceholderIds.SchoolId, admin.SchoolId);
        Assert.False(string.IsNullOrWhiteSpace(admin.Name));
    }

    [Fact]
    public async Task Seed_WhenTheSchoolHasAnAdmin_ChangesNothing()
    {
        await DeactivateAdminsAsync();
        var first = $"forsta.{Guid.NewGuid():N}@skolan.se";
        var second = $"andra.{Guid.NewGuid():N}@skolan.se";
        await SeedAsync(new FirstAdminOptions { Email = first, Password = _password });

        await SeedAsync(new FirstAdminOptions { Email = second, Password = _password });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.Users.AnyAsync(user => user.Email == second));
    }

    [Fact]
    public async Task Seed_ForAnExistingUser_MakesHerAdminWithThePassword()
    {
        await DeactivateAdminsAsync();
        var email = $"larare.{Guid.NewGuid():N}@skolan.se";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.Users.Add(new User
            {
                SchoolId = PlaceholderIds.SchoolId,
                Name = "Befintlig Lärare",
                Email = email,
                Role = UserRole.Teacher
            });
            await db.SaveChangesAsync();
        }

        await SeedAsync(new FirstAdminOptions { Email = email.ToUpperInvariant(), Password = _password });

        using var check = factory.Services.CreateScope();
        var users = check.ServiceProvider.GetRequiredService<AppDb>().Users.AsNoTracking();
        var user = await users.SingleAsync(row => row.Email == email);
        Assert.Equal((UserRole.Admin, "Befintlig Lärare"), (user.Role, user.Name));
        Assert.NotNull(user.PasswordHash);
    }

    [Theory]
    [InlineData("inte-en-adress", _password)]
    [InlineData("rektor@skolan.se", "kort")]
    [InlineData("", _password)]
    public void Startup_WithInvalidSettings_Stops(string email, string password)
    {
        using var broken = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:FirstAdmin:Email", email);
            builder.UseSetting("Auth:FirstAdmin:Password", password);
        });

        Assert.Throws<OptionsValidationException>(() => broken.CreateClient());
    }

    private async Task SeedAsync(FirstAdminOptions options)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var seed = new FirstAdminSeed(
            services.GetRequiredService<AppDb>(),
            services.GetRequiredService<IPasswordHasher<User>>(),
            Options.Create(options),
            NullLogger<FirstAdminSeed>.Instance);
        await seed.SeedAsync(CancellationToken.None);
    }

    // Som en ny installation: ingen admin kan logga in.
    private async Task DeactivateAdminsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Users
            .Where(user => user.Role == UserRole.Admin && user.DeactivatedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DeactivatedAt, DateTimeOffset.UtcNow));
    }
}
