using Backend.Application.Abstractions;
using Backend.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace backend.Tests;

// Låter ett test vara en annan användare, med headern X-Test-User. Utan headern är det samma användare
// som resten av appen (PlaceholderCurrentUser), så övriga tester påverkas inte.
public class HeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string Header = "X-Test-User";

    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.Request.Headers[Header], out var userId)
            ? userId
            : PlaceholderCurrentUser.PlaceholderUserId;
}
