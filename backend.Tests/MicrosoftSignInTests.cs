using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Backend.Application.Abstractions;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace backend.Tests;

/// <summary>
/// Microsoft-inloggning med riktig JWT. ID-tokens signeras med en testnyckel, och appen får den nyckeln i stället
/// för att hämta Microsofts metadata, så kontrollen av tokenet är densamma som i prod.
/// </summary>
public sealed class MicrosoftTestWebApplicationFactory : TestWebApplicationFactory
{
    public const string ClientId = "11111111-2222-3333-4444-555555555555";
    public const string TenantId = "66666666-7777-8888-9999-000000000000";

    public static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "test-key" };

    public MicrosoftTestWebApplicationFactory() => UseTestAuthentication = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:Entra:ClientId", ClientId);
        builder.UseSetting("Auth:Entra:TenantId", TenantId);
        builder.ConfigureTestServices(services =>
        {
            var configuration = new OpenIdConnectConfiguration();
            configuration.SigningKeys.Add(SigningKey);
            services.RemoveAll<IConfigurationManager<OpenIdConnectConfiguration>>();
            services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(
                new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration));
        });
    }
}

public class MicrosoftSignInTests(MicrosoftTestWebApplicationFactory factory)
    : IClassFixture<MicrosoftTestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record SignInResponse(string AccessToken, DateTimeOffset ExpiresAt, bool FirstSignIn);

    private record MeResponse(Guid Id, string Name, string Email, string Role);

    [Fact]
    public async Task Providers_ReturnsTheMicrosoftApp()
    {
        var providers = await _client.GetStringAsync("/api/auth/providers");

        Assert.Contains(MicrosoftTestWebApplicationFactory.ClientId, providers);
        Assert.Contains(MicrosoftTestWebApplicationFactory.TenantId, providers);
    }

    [Fact]
    public async Task FirstSignIn_CreatesAStudent_InTheClassFromTheEmail_ThenFindsThemByOid()
    {
        var (schoolId, classId) = await AddLinkedSchoolAsync(className: ".NET25");
        var oid = Guid.NewGuid().ToString();
        var email = $"ny.elev{Guid.NewGuid():N}.net25@skola.se";
        var token = IdToken(oid, email, "Ny Elev");

        var first = await SignInAsync(token);
        var second = await SignInAsync(IdToken(oid, "bytt.adress@skola.se", "Ny Elev"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<SignInResponse>();
        Assert.True(firstBody!.FirstSignIn);
        var me = await MeAsync(firstBody.AccessToken);
        Assert.Equal(email, me.Email);
        Assert.Equal("Student", me.Role);
        Assert.False((await second.Content.ReadFromJsonAsync<SignInResponse>())!.FirstSignIn);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await db.Users.SingleAsync(row => row.ExternalId == oid);
        Assert.Equal(schoolId, user.SchoolId);
        Assert.True(await db.Enrollments.AnyAsync(row =>
            row.UserId == user.Id && row.GroupId == classId && row.Role == GroupRole.Student));
    }

    [Fact]
    public async Task FirstSignIn_WithoutAMatchingClass_CreatesAStudentWithoutClass()
    {
        await AddLinkedSchoolAsync(className: "TE22A");
        var oid = Guid.NewGuid().ToString();

        var response = await SignInAsync(IdToken(oid, $"elev{Guid.NewGuid():N}.fib25@skola.se", "Utan Klass"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await db.Users.SingleAsync(row => row.ExternalId == oid);
        Assert.False(await db.Enrollments.AnyAsync(row => row.UserId == user.Id));
    }

    [Fact]
    public async Task FirstSignIn_LinksTheUserAdminAddedWithTheSameEmail_AndKeepsTheirRole()
    {
        var (schoolId, _) = await AddLinkedSchoolAsync();
        var email = $"larare{Guid.NewGuid():N}@skola.se";
        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var teacher = new User { SchoolId = schoolId, Name = "Lärare", Email = email, Role = UserRole.Teacher };
            db.Users.Add(teacher);
            await db.SaveChangesAsync();
            userId = teacher.Id;
        }

        var response = await SignInAsync(IdToken(Guid.NewGuid().ToString(), email.ToUpperInvariant(), "Lärare"));

        var me = await MeAsync((await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        Assert.Equal(userId, me.Id);
        Assert.Equal("Teacher", me.Role);
    }

    [Fact]
    public async Task SignIn_WithAnEmailUsedInAnotherSchoolOrLinkedToAnotherAccount_IsForbidden()
    {
        await AddLinkedSchoolAsync();
        var email = $"tagen{Guid.NewGuid():N}@skola.se";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var other = await TestData.AddUserAsync(db);
            other.Email = email;
            await db.SaveChangesAsync();
        }

        var response = await SignInAsync(IdToken(Guid.NewGuid().ToString(), email, "Någon"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("används redan", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SignIn_AsABlockedUser_IsForbidden()
    {
        var (schoolId, _) = await AddLinkedSchoolAsync();
        var oid = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.Users.Add(new User
            {
                SchoolId = schoolId,
                Name = "Spärrad",
                Email = $"sparrad{Guid.NewGuid():N}@skola.se",
                ExternalId = oid,
                DeactivatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await SignInAsync(IdToken(oid, "sparrad@skola.se", "Spärrad"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(AuthService.DeactivatedMessage, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SignIn_WhenNoSchoolHasLinkedTheTenant_IsForbidden()
    {
        await UnlinkAllSchoolsAsync();

        var response = await SignInAsync(IdToken(Guid.NewGuid().ToString(), "x@skola.se", "X"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(MicrosoftSignInService.SchoolNotLinkedMessage, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("wrong-audience")]
    [InlineData("wrong-tenant")]
    [InlineData("expired")]
    [InlineData("wrong-key")]
    [InlineData("garbage")]
    public async Task SignIn_WithAnInvalidToken_Returns401(string problem)
    {
        await AddLinkedSchoolAsync();
        var oid = Guid.NewGuid().ToString();
        var token = problem switch
        {
            "wrong-audience" => IdToken(oid, "a@skola.se", "A", audience: Guid.NewGuid().ToString()),
            "wrong-tenant" => IdToken(oid, "a@skola.se", "A", tenant: Guid.NewGuid().ToString()),
            "expired" => IdToken(oid, "a@skola.se", "A", expires: DateTime.UtcNow.AddMinutes(-10)),
            "wrong-key" => IdToken(oid, "a@skola.se", "A", key: new RsaSecurityKey(RSA.Create(2048))),
            _ => "inte.ett.token"
        };

        var response = await SignInAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDb>().Users
            .AnyAsync(user => user.ExternalId == oid));
    }

    [Theory]
    [InlineData("christian.gennari.net25@edu.edugrade.se", "net25")]
    [InlineData("Anna.Svensson.FIB25@skola.se", "fib25")]
    [InlineData("anna.svensson@skola.se", null)]
    [InlineData("anna@skola.se", null)]
    [InlineData("anna.svensson.2@skola.se", null)]
    public void ClassCodeFromEmail_TakesTheLastPartWithLettersAndDigits(string email, string? expected)
    {
        Assert.Equal(expected, MicrosoftSignInService.ClassCodeFromEmail(email));
    }

    [Fact]
    public async Task Admin_LinksAndUnlinksTheSchool_AndSignInFollows()
    {
        await UnlinkAllSchoolsAsync();
        var (schoolId, admin) = await AddSchoolWithAdminAsync();
        var oid = Guid.NewGuid().ToString();
        var token = IdToken(oid, $"elev{Guid.NewGuid():N}@skola.se", "Elev");

        var before = await AsAsync(admin, HttpMethod.Get, $"/api/schools/{schoolId}/microsoft");
        var blocked = await SignInAsync(token);
        var linked = await AsAsync(admin, HttpMethod.Put, $"/api/schools/{schoolId}/microsoft");
        var allowed = await SignInAsync(token);
        var unlinked = await AsAsync(admin, HttpMethod.Delete, $"/api/schools/{schoolId}/microsoft");
        var blockedAgain = await SignInAsync(token);

        Assert.Contains("\"linked\":false", await before.Content.ReadAsStringAsync());
        Assert.Contains("\"configured\":true", await before.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("\"linked\":true", await linked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("\"linked\":false", await unlinked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, blockedAgain.StatusCode);
    }

    [Fact]
    public async Task LinkSchool_WhenAnotherSchoolHasTheTenant_ReturnsConflict_AndNonAdminIsForbidden()
    {
        await AddLinkedSchoolAsync();
        var (schoolId, admin) = await AddSchoolWithAdminAsync();
        Guid teacherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            teacherId = (await TestData.AddUserAsync(db, schoolId, UserRole.Teacher)).Id;
        }

        var conflict = await AsAsync(admin, HttpMethod.Put, $"/api/schools/{schoolId}/microsoft");
        var forbidden = await AsAsync(teacherId, HttpMethod.Put, $"/api/schools/{schoolId}/microsoft");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task UnlinkUser_ClearsTheOid_AndTheNextSignInLinksByEmailAgain()
    {
        var (schoolId, _) = await AddLinkedSchoolAsync();
        Guid admin;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            admin = (await TestData.AddUserAsync(db, schoolId, UserRole.Admin)).Id;
        }

        var email = $"bytt{Guid.NewGuid():N}@skola.se";
        var first = await SignInAsync(IdToken(Guid.NewGuid().ToString(), email, "Bytt Konto"));
        var userId = (await MeAsync((await first.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken)).Id;

        var unlink = await AsAsync(admin, HttpMethod.Delete, $"/api/users/{userId}/microsoft");
        var newAccount = await SignInAsync(IdToken(Guid.NewGuid().ToString(), email, "Bytt Konto"));

        Assert.Contains("\"microsoftLinked\":false", await unlink.Content.ReadAsStringAsync());
        var me = await MeAsync((await newAccount.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        Assert.Equal(userId, me.Id);
    }

    [Fact]
    public async Task AdminOverview_ListsStudentsWithoutClass_AndNobodyWithoutPasswordWhenLinked()
    {
        var (schoolId, _) = await AddLinkedSchoolAsync(className: ".NET25");
        Guid admin;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            admin = (await TestData.AddUserAsync(db, schoolId, UserRole.Admin)).Id;
        }

        await SignInAsync(IdToken(Guid.NewGuid().ToString(), $"i.klass{Guid.NewGuid():N}.net25@skola.se", "I Klass"));
        await SignInAsync(IdToken(Guid.NewGuid().ToString(), $"utan{Guid.NewGuid():N}@skola.se", "Utan Klass"));

        var overview = await (await AsAsync(admin, HttpMethod.Get, "/api/me/admin-overview"))
            .Content.ReadAsStringAsync();

        Assert.Contains("Utan Klass", overview);
        Assert.DoesNotContain("I Klass", overview);
        Assert.Contains("\"usersWithoutPassword\":[]", overview);
    }

    private Task<HttpResponseMessage> SignInAsync(string idToken) =>
        _client.PostAsJsonAsync("/api/auth/microsoft", new { idToken });

    private async Task<MeResponse> MeAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MeResponse>())!;
    }

    // Ett anrop som användaren, med ett riktigt Lärka-token.
    private async Task<HttpResponseMessage> AsAsync(Guid userId, HttpMethod method, string path)
    {
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var user = await db.Users.SingleAsync(row => row.Id == userId);
            token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateToken(user);
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<(Guid SchoolId, Guid AdminId)> AddSchoolWithAdminAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        return (school.Id, admin.Id);
    }

    // Testklassens tenant hör till en ny skola, med en klass om className ges. Tidigare skolor kopplas loss.
    private async Task<(Guid SchoolId, Guid? ClassId)> AddLinkedSchoolAsync(string? className = null)
    {
        await UnlinkAllSchoolsAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        school.EntraTenantId = MicrosoftTestWebApplicationFactory.TenantId;
        Guid? classId = null;
        if (className is not null)
        {
            var group = new Group { SchoolId = school.Id, Type = GroupType.Class, Name = className, Term = "HT25" };
            db.Groups.Add(group);
            classId = group.Id;
        }

        await db.SaveChangesAsync();
        return (school.Id, classId);
    }

    private async Task UnlinkAllSchoolsAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDb>().Schools
            .Where(school => school.EntraTenantId != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(school => school.EntraTenantId, (string?)null));
    }

    private static string IdToken(
        string oid,
        string email,
        string name,
        string? audience = null,
        string? tenant = null,
        DateTime? expires = null,
        SecurityKey? key = null)
    {
        var tid = tenant ?? MicrosoftTestWebApplicationFactory.TenantId;
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(30);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = $"https://login.microsoftonline.com/{tid}/v2.0",
            Audience = audience ?? MicrosoftTestWebApplicationFactory.ClientId,
            IssuedAt = expiresAt.AddMinutes(-60),
            NotBefore = expiresAt.AddMinutes(-60),
            Expires = expiresAt,
            Subject = new ClaimsIdentity([
                new Claim("oid", oid),
                new Claim("tid", tid),
                new Claim("preferred_username", email),
                new Claim("name", name)
            ]),
            SigningCredentials = new SigningCredentials(
                key ?? MicrosoftTestWebApplicationFactory.SigningKey, SecurityAlgorithms.RsaSha256)
        });
    }
}
