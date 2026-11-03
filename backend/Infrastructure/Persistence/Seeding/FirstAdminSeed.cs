using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Auth;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Skapar skolans första admin från Auth:FirstAdmin när skolan inte har någon admin som kan logga in (med lösenord
/// eller Microsoft). Det behövs när en skola kör Lärka utan Microsoft-inloggning, eftersom ingen annan väg ger en
/// första admin. Finns redan en sådan admin händer ingenting, så inställningen kan ligga kvar. Lösenordet loggas aldrig.
/// </summary>
public sealed class FirstAdminSeed(
    AppDb db,
    IPasswordHasher<User> passwordHasher,
    IOptions<FirstAdminOptions> options,
    ILogger<FirstAdminSeed> logger)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return;
        }

        var school = await db.Schools.FirstOrDefaultAsync(row => row.Id == PlaceholderIds.SchoolId, ct)
                     ?? await db.Schools.OrderBy(row => row.Name).FirstOrDefaultAsync(ct);
        if (school is null)
        {
            logger.LogWarning("Ingen skola finns, så Auth:FirstAdmin skapar ingen admin.");
            return;
        }

        // Platshållarens admin har varken lösenord eller Microsoft-koppling och räknas inte.
        var hasAdmin = await db.Users.AnyAsync(
            user => user.SchoolId == school.Id
                && user.Role == UserRole.Admin
                && user.DeactivatedAt == null
                && (user.PasswordHash != null || user.ExternalId != null),
            ct);
        if (hasAdmin)
        {
            logger.LogInformation("Skolan {School} har redan en admin. Auth:FirstAdmin används inte.", school.Name);
            return;
        }

        var email = settings.Email.Trim();
        var admin = await db.Users.FirstOrDefaultAsync(user => user.Email.ToLower() == email.ToLower(), ct);
        if (admin is null)
        {
            admin = new User
            {
                SchoolId = school.Id,
                Email = email,
                Name = string.IsNullOrWhiteSpace(settings.Name)
                    ? SchoolAddress.NameFromEmail(email) ?? email
                    : settings.Name.Trim()
            };
            db.Users.Add(admin);
        }

        admin.Role = UserRole.Admin;
        admin.DeactivatedAt = null;
        admin.PasswordHash = passwordHasher.HashPassword(admin, settings.Password);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Första admin {Email} är klar i skolan {School}.", email, school.Name);
    }
}
