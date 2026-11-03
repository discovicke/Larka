using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class SchoolService(
    ISchoolRepository schools,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    IMicrosoftTokenValidator microsoft,
    IUserRepository users,
    IUnitOfWork unitOfWork)
{
    public const string NoPasswordAdminMessage =
        "Ge minst en admin ett lösenord först. Annars kan ingen admin logga in när Microsoft är av.";

    // Användaren ser bara sin egen skola, så listan har högst en rad.
    public async Task<List<SchoolDto>> GetAllAsync(CancellationToken ct)
    {
        var school = await GetAsync(await currentSchool.GetIdAsync(ct) ?? Guid.Empty, ct);
        return school is null
            ? []
            : [school];
    }

    /// <summary>Null (404) om skolan inte finns eller är en annan skola än användarens.</summary>
    public async Task<SchoolDto?> GetAsync(Guid id, CancellationToken ct)
    {
        if (!await currentSchool.OwnsAsync(id, ct))
        {
            return null;
        }

        var school = await schools.GetAsync(id, ct);
        return school is null
            ? null
            : SchoolDto.From(school);
    }

    public async Task<ServiceResult<SchoolDto>> CreateAsync(CreateSchoolDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<SchoolDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ServiceResult<SchoolDto>.Invalid("name is required");
        }

        var school = new School { Name = dto.Name.Trim() };
        schools.Add(school);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SchoolDto>.Ok(SchoolDto.From(school));
    }

    // Microsoft-inloggning för skolan. Appens tenant (Auth:Entra) kopplas till skolan, så att inloggningar därifrån
    // hamnar här. Bara admin i skolan.
    public async Task<ServiceResult<SchoolMicrosoftDto>> GetMicrosoftAsync(Guid id, CancellationToken ct)
    {
        var school = await GetOwnAsAdminAsync(id, ct);
        if (school.Status != ResultStatus.Ok)
        {
            return ServiceResult<SchoolMicrosoftDto>.From(school);
        }

        return ServiceResult<SchoolMicrosoftDto>.Ok(MicrosoftOf(school.Value!));
    }

    // 404 när Microsoft-inloggning inte är inställd, 409 när en annan skola redan har tenanten.
    public async Task<ServiceResult<SchoolMicrosoftDto>> LinkMicrosoftAsync(Guid id, CancellationToken ct)
    {
        var school = await GetOwnAsAdminAsync(id, ct);
        if (school.Status != ResultStatus.Ok)
        {
            return ServiceResult<SchoolMicrosoftDto>.From(school);
        }

        var settings = microsoft.Settings;
        if (settings is null)
        {
            return ServiceResult<SchoolMicrosoftDto>.NotFound("microsoft sign-in is not set up");
        }

        var owner = await schools.GetByEntraTenantAsync(settings.TenantId, ct);
        if (owner is not null && owner.Id != id)
        {
            return ServiceResult<SchoolMicrosoftDto>.Conflict("another school is linked to the tenant");
        }

        school.Value!.EntraTenantId = settings.TenantId;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SchoolMicrosoftDto>.Ok(MicrosoftOf(school.Value));
    }

    // Utan Microsoft loggar alla in med lösenord, så minst en admin måste ha ett. Annars kan ingen slå på det igen.
    // Användarnas koppling till Microsoft (oid) ligger kvar, så de känns igen om skolan kopplas igen.
    public async Task<ServiceResult<SchoolMicrosoftDto>> UnlinkMicrosoftAsync(Guid id, CancellationToken ct)
    {
        var school = await GetOwnAsAdminAsync(id, ct);
        if (school.Status != ResultStatus.Ok)
        {
            return ServiceResult<SchoolMicrosoftDto>.From(school);
        }

        if (!await users.HasPasswordAdminAsync(id, ct))
        {
            return ServiceResult<SchoolMicrosoftDto>.Conflict(NoPasswordAdminMessage);
        }

        school.Value!.EntraTenantId = null;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SchoolMicrosoftDto>.Ok(MicrosoftOf(school.Value));
    }

    private SchoolMicrosoftDto MicrosoftOf(School school)
    {
        var settings = microsoft.Settings;
        return new SchoolMicrosoftDto(
            settings is not null,
            settings is not null && school.EntraTenantId == settings.TenantId,
            settings?.TenantId);
    }

    private async Task<ServiceResult<School>> GetOwnAsAdminAsync(Guid id, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<School>.Forbidden();
        }

        var school = await schools.GetAsync(id, ct);
        return school is null || !await currentSchool.OwnsAsync(id, ct)
            ? ServiceResult<School>.NotFound("school not found")
            : ServiceResult<School>.Ok(school);
    }
}
