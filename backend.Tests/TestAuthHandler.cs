using System.Security.Claims;
using System.Text.Encodings.Web;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Tests;

// Testens inloggning: varje anrop är inloggat som användaren i X-Test-User, eller placeholder-admin utan
// headern. Då behöver testerna inte ett riktigt token för varje anrop. Tester som prövar JWT använder
// JwtTestWebApplicationFactory i stället.
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers[Header].ToString();
        var userId = Guid.TryParse(header, out var parsed)
            ? parsed
            : PlaceholderIds.UserId;

        var identity = new ClaimsIdentity([new Claim("sub", userId.ToString())], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
