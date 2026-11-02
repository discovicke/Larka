using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

/// <summary>
/// En ny skolas installation: prod utan demodata, och infra/entra-setup.sh har sparat vem som körde det
/// (AdminObjectId) och organisationens namn.
/// </summary>
public sealed class NewSchoolWebApplicationFactory : MicrosoftTestWebApplicationFactory
{
    public const string AdminObjectId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    public const string SchoolName = "Nya skolan";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
        builder.UseSetting("Auth:Entra:AdminObjectId", AdminObjectId);
        builder.UseSetting("Auth:Entra:SchoolName", SchoolName);
    }
}

public class NewSchoolSetupTests(NewSchoolWebApplicationFactory factory)
    : IClassFixture<NewSchoolWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task NewSchool_HasNoDemoAccounts()
    {
        using var scope = factory.Services.CreateScope();
        var emails = await scope.ServiceProvider.GetRequiredService<AppDb>().Users
            .Select(user => user.Email)
            .ToListAsync();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "admin" });

        Assert.DoesNotContain("admin", emails);
        Assert.DoesNotContain("larare", emails);
        Assert.DoesNotContain("elev", emails);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task SetupAdmin_LinksAndNamesTheSchool_AndBecomesAdmin_ThenOthersBecomeStudents()
    {
        var blocked = await SignInAsync(Guid.NewGuid().ToString(), "tidig@nyaskolan.se");
        var admin = await SignInAsync(NewSchoolWebApplicationFactory.AdminObjectId, "it.admin@nyaskolan.se");
        var student = await SignInAsync(Guid.NewGuid().ToString(), "elev@nyaskolan.se");
        var providers = await _client.GetStringAsync("/api/auth/providers");

        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, student.StatusCode);
        Assert.Contains($"\"schoolName\":\"{NewSchoolWebApplicationFactory.SchoolName}\"", providers);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await db.Schools.SingleAsync(row => row.Id == PlaceholderIds.SchoolId);
        var users = await db.Users
            .Where(user => user.Email.EndsWith("@nyaskolan.se"))
            .ToDictionaryAsync(user => user.Email, user => (user.Role, user.SchoolId));
        Assert.Equal(MicrosoftTestWebApplicationFactory.TenantId, school.EntraTenantId);
        Assert.Equal(NewSchoolWebApplicationFactory.SchoolName, school.Name);
        Assert.Equal((UserRole.Admin, school.Id), users["it.admin@nyaskolan.se"]);
        Assert.Equal((UserRole.Student, school.Id), users["elev@nyaskolan.se"]);
        Assert.False(users.ContainsKey("tidig@nyaskolan.se"));
    }

    [Fact]
    public async Task SetupAdmin_RelinksASchoolThatPointsAtAnOldTenant()
    {
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDb>().Schools
                .Where(row => row.Id == PlaceholderIds.SchoolId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.EntraTenantId, "gammal-tenant"));
        }

        var blocked = await SignInAsync(Guid.NewGuid().ToString(), "annan@nyaskolan.se");
        var admin = await SignInAsync(NewSchoolWebApplicationFactory.AdminObjectId, "it.admin@nyaskolan.se");

        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        using var check = factory.Services.CreateScope();
        var school = await check.ServiceProvider.GetRequiredService<AppDb>().Schools
            .SingleAsync(row => row.Id == PlaceholderIds.SchoolId);
        Assert.Equal(MicrosoftTestWebApplicationFactory.TenantId, school.EntraTenantId);
    }

    private Task<HttpResponseMessage> SignInAsync(string oid, string email) =>
        _client.PostAsJsonAsync(
            "/api/auth/microsoft",
            new { idToken = MicrosoftSignInTests.IdToken(oid, email, email) });
}
