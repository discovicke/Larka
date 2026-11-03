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
// tenanten och hen aldrig har loggat in kopplar hens första inloggning skolan och ger den organisationens namn, och
// hen skapas som admin. Så kommer en ny skola igång utan lösenord och utan att någon först loggar in på annat sätt.
// Efter det är inloggningen av tills admin slår på den igen, även för hen.
public class MicrosoftSignInService(
    IMicrosoftTokenValidator validator,
    ISchoolRepository schools,
    IUserRepository users,
    IGroupRepository groups,
    ITokenService tokens,
    IUnitOfWork unitOfWork)
{
    public const string SchoolNotLinkedMessage = "Skolan har inte slagit på inloggning med Microsoft.";
    public const string EmailTakenMessage = "E-postadressen används redan av ett annat konto. Kontakta skolans admin.";

    // Microsoft finns med bara när en skola har slagit på inloggningen, eller när en ny skola väntar på sin första
    // admin. Annars visar inloggningssidan bara lösenordet.
    public async Task<AuthProvidersDto> GetProvidersAsync(CancellationToken ct)
    {
        var settings = validator.Settings;
        var linked = settings is null
            ? null
            : await schools.GetByEntraTenantAsync(settings.TenantId, ct);
        var school = linked ?? await schools.GetDefaultAsync(ct);
        var shown = settings is not null && (linked is not null || await AwaitsSetupAdminAsync(settings, ct));
        return new AuthProvidersDto(
            shown
                ? new MicrosoftProviderDto(settings!.ClientId, settings.TenantId)
                : null,
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
        if (school is null && isSetupAdmin && await AwaitsSetupAdminAsync(settings, ct))
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
                    Name = SchoolAddress.DisplayName(identity.Name, identity.Email),
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

    // En ny skola: setup-admin finns i inställningarna men har aldrig loggat in. Har hen loggat in en gång har skolan
    // kommit igång, och då är det admins val om inloggningen är på.
    private async Task<bool> AwaitsSetupAdminAsync(MicrosoftSignInSettings settings, CancellationToken ct) =>
        settings.AdminObjectId is not null && await users.GetByExternalIdAsync(settings.AdminObjectId, ct) is null;

    // Skolan från migrationen. Anropas bara när ingen skola har tenanten, så en tenant som skolan redan har är en
    // gammal (till exempel när appen har flyttats till en ny tenant). Ingen kan logga in med den, och den byts ut.
    // Sparas med användaren.
    private async Task<School?> LinkDefaultSchoolAsync(MicrosoftSignInSettings settings, CancellationToken ct)
    {
        var school = await schools.GetDefaultAsync(ct);
        if (school is null)
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

    // Bara om exakt en klass i skolan har namnet, utan hänsyn till versaler och tecken: ".NET25" och "NET 25" blir
    // "net25".
    private async Task JoinClassFromEmailAsync(User user, CancellationToken ct)
    {
        var code = SchoolAddress.ClassCode(user.Email);
        if (code is null)
        {
            return;
        }

        var matches = (await groups.GetForSchoolAsync(user.SchoolId, ct))
            .Where(group => group.Type == GroupType.Class && SchoolAddress.NormalizeCode(group.Name) == code)
            .ToList();
        if (matches.Count == 1)
        {
            groups.AddMember(new Enrollment { GroupId = matches[0].Id, UserId = user.Id, Role = GroupRole.Student });
        }
    }
}
