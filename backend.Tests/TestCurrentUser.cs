using System.Security.Claims;
using Backend.Application.Abstractions;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Http;

namespace backend.Tests;

// Testerna loggar in via TestAuthHandler, men en del tjänster testas direkt i en scope utan HTTP-förfrågan.
// Då finns ingen HttpContext, och vi faller tillbaka på placeholder-admin.
public class TestCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var userId)
                ? userId
                : PlaceholderIds.UserId;
        }
    }
}
