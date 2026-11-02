using System.Text.RegularExpressions;
using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Inloggning med skolans Microsoft-konto. Klienten loggar in hos Microsoft (MSAL) och skickar ID-tokenet hit en gång.
// Här byts det mot Lärkas eget token, så resten av API:et vet inte hur användaren loggade in.
//
// Användaren hittas på Microsofts id (oid). Första gången kopplas en användare som admin har lagt in med samma
// e-post i skolan, annars skapas en ny elev. Ingen väljer själv sin roll eller klass: en ny elev hamnar bara i
// klassen som adressen slutar på (fornamn.efternamn.net25@... -> .NET25), om skolan har den.
//
// Undantaget är den som körde infra/entra-setup.sh (Auth:Entra:AdminObjectId). När ingen skola är kopplad till
// tenanten än kopplar hens första inloggning skolan och ger den organisationens namn, och hen skapas som admin.
// Så kommer en ny skola igång utan lösenord och utan att någon först loggar in på annat sätt.
public partial class MicrosoftSignInService(
    IMicrosoftTokenValidator validator,
    ISchoolRepository schools,
    IUserRepository users,
    IGroupRepository groups,
    ITokenService tokens,
    IUnitOfWork unitOfWork)
{
    public const string SchoolNotLinkedMessage = "Skolan har inte slagit på inloggning med Microsoft.";
    public const string EmailTakenMessage = "E-postadressen används redan av ett annat konto. Kontakta skolans admin.";

    public async Task<AuthProvidersDto> GetProvidersAsync(CancellationToken ct)
    {
        var settings = validator.Settings;
        var school = settings is null
            ? null
            : await schools.GetByEntraTenantAsync(settings.TenantId, ct);
        school ??= await schools.GetDefaultAsync(ct);
        return new AuthProvidersDto(
            settings is null
                ? null
                : new MicrosoftProviderDto(settings.ClientId, settings.TenantId),
            school?.Name);
    }

    public async Task<ServiceResult<MicrosoftLoginResultDto>> SignInAsync(MicrosoftLoginDto dto, CancellationToken ct)
    {
        var settings = validator.Settings;
        if (settings is null)
        {
            return ServiceResult<MicrosoftLoginResultDto>.NotFound("microsoft sign-in is not set up");
        }

        var identity = await validator.ValidateAsync(dto.IdToken, ct);
        if (identity is null)
        {
            return ServiceResult<MicrosoftLoginResultDto>.Invalid("invalid id token");
        }

        var isSetupAdmin = settings.AdminObjectId is not null && identity.ObjectId == settings.AdminObjectId;
        var school = await schools.GetByEntraTenantAsync(identity.TenantId, ct);
        var linkedSchool = false;
        if (school is null && isSetupAdmin)
        {
            school = await LinkDefaultSchoolAsync(settings, ct);
            linkedSchool = school is not null;
        }

        if (school is null)
        {
            return ServiceResult<MicrosoftLoginResultDto>.Forbidden(SchoolNotLinkedMessage);
        }

        var firstSignIn = false;
        var user = await users.GetByExternalIdAsync(identity.ObjectId, ct);
        if (user is null)
        {
            var email = identity.Email.Trim().ToLowerInvariant();
            user = await users.GetByEmailAsync(email, ct);
            if (user is not null && (user.SchoolId != school.Id || user.ExternalId is not null))
            {
                return ServiceResult<MicrosoftLoginResultDto>.Forbidden(EmailTakenMessage);
            }

            if (user is null)
            {
                user = new User
                {
                    SchoolId = school.Id,
                    Name = identity.Name.Trim(),
                    Email = email,
                    Role = isSetupAdmin
                        ? UserRole.Admin
                        : UserRole.Student
                };
                users.Add(user);
                if (!isSetupAdmin)
                {
                    await JoinClassFromEmailAsync(user, ct);
                }
            }

            user.ExternalId = identity.ObjectId;
            firstSignIn = true;
        }

        if (user.DeactivatedAt is not null)
        {
            return ServiceResult<MicrosoftLoginResultDto>.Forbidden(AuthService.DeactivatedMessage);
        }

        if (firstSignIn || linkedSchool)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }

        return ServiceResult<MicrosoftLoginResultDto>.Ok(
            new MicrosoftLoginResultDto(tokens.CreateToken(user), tokens.ExpiresAt, firstSignIn));
    }

    // Skolan från migrationen, om den inte redan hör till en annan tenant. Sparas med användaren.
    private async Task<School?> LinkDefaultSchoolAsync(MicrosoftSignInSettings settings, CancellationToken ct)
    {
        var school = await schools.GetDefaultAsync(ct);
        if (school is null || school.EntraTenantId is not null)
        {
            return null;
        }

        school.EntraTenantId = settings.TenantId;
        if (settings.SchoolName is not null)
        {
            school.Name = settings.SchoolName;
        }

        return school;
    }

    /// <summary>
    /// Klasskoden i adressen: sista delen före @ efter en punkt, och bara om den har en siffra, så att ett
    /// efternamn aldrig räknas som klass. "christian.gennari.net25@edu.edugrade.se" ger "net25".
    /// </summary>
    public static string? ClassCodeFromEmail(string email)
    {
        var local = email.Split('@')[0];
        var dot = local.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var code = Normalize(local[(dot + 1)..]);
        return code.Any(char.IsDigit) && code.Any(char.IsLetter)
            ? code
            : null;
    }

    // Bara om exakt en klass i skolan har namnet, utan hänsyn till versaler och tecken: ".NET25" och "NET 25" blir
    // "net25".
    private async Task JoinClassFromEmailAsync(User user, CancellationToken ct)
    {
        var code = ClassCodeFromEmail(user.Email);
        if (code is null)
        {
            return;
        }

        var matches = (await groups.GetForSchoolAsync(user.SchoolId, ct))
            .Where(group => group.Type == GroupType.Class && Normalize(group.Name) == code)
            .ToList();
        if (matches.Count == 1)
        {
            groups.AddMember(new Enrollment { GroupId = matches[0].Id, UserId = user.Id, Role = GroupRole.Student });
        }
    }

    private static string Normalize(string text) => NonAlphanumeric().Replace(text, "").ToLowerInvariant();

    [GeneratedRegex("[^\\p{L}\\p{N}]")]
    private static partial Regex NonAlphanumeric();
}
