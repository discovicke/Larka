using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

public class GroupService(
    IGroupRepository groups,
    ICourseRepository courses,
    IUserRepository users,
    IPermissions permissions,
    ICurrentUser currentUser,
    ICurrentSchool currentSchool,
    IUnitOfWork unitOfWork)
{
    public async Task<List<GroupDto>> GetAllAsync(CancellationToken ct)
    {
        var schoolId = await currentSchool.GetIdAsync(ct);
        if (schoolId is null)
        {
            return [];
        }

        // Admin ser hela skolan, alla andra bara grupperna de är medlemmar i.
        var schoolGroups = await permissions.IsAdminAsync(ct)
            ? await groups.GetForSchoolAsync(schoolId.Value, ct)
            : await groups.GetForMemberAsync(schoolId.Value, currentUser.UserId, ct);
        var counts = await groups.GetMemberCountsAsync(schoolId.Value, ct);
        return schoolGroups
            .Select(group => GroupDto.From(group, counts.GetValueOrDefault(group.Id)))
            .ToList();
    }

    public async Task<GroupDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var group = await GetOwnAsync(id, ct);
        return group is null
            ? null
            : GroupDto.From(group, await groups.CountMembersAsync(id, ct));
    }

    // Typen är en enum i DTO:n, så okända typer stoppas redan när JSON läses in (400).
    public async Task<ServiceResult<GroupDto>> CreateAsync(CreateGroupDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GroupDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ServiceResult<GroupDto>.Invalid("name is required");
        }

        // Bara i den egna skolan, annars skulle admin skapa rader som den sedan inte ser.
        if (!await currentSchool.OwnsAsync(dto.SchoolId, ct))
        {
            return ServiceResult<GroupDto>.Invalid("school does not exist");
        }

        var name = dto.Name.Trim();
        var term = dto.Term?.Trim() ?? "";
        if (await groups.NameExistsAsync(dto.SchoolId, name, term, ct))
        {
            return ServiceResult<GroupDto>.Invalid("the school already has a group with this name and term");
        }

        var group = new Group { SchoolId = dto.SchoolId, Type = dto.Type, Name = name, Term = term };
        groups.Add(group);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<GroupDto>.Ok(GroupDto.From(group, 0));
    }

    public async Task<ServiceResult<GroupDto>> UpdateAsync(Guid id, UpdateGroupDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<GroupDto>.Forbidden();
        }

        var group = await GetOwnAsync(id, ct);
        if (group is null)
        {
            return ServiceResult<GroupDto>.NotFound("group not found");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ServiceResult<GroupDto>.Invalid("name is required");
        }

        var name = dto.Name.Trim();
        var term = dto.Term?.Trim() ?? "";
        var renamed = name != group.Name || term != group.Term;
        if (renamed && await groups.NameExistsAsync(group.SchoolId, name, term, ct))
        {
            return ServiceResult<GroupDto>.Invalid("the school already has a group with this name and term");
        }

        // En committee läser inga kurser, så kopplingarna måste tas bort först.
        if (dto.Type == GroupType.Committee && (await groups.GetCoursesAsync(id, ct)).Count > 0)
        {
            return ServiceResult<GroupDto>.Invalid("a committee cannot read courses, unlink them first");
        }

        group.Type = dto.Type;
        group.Name = name;
        group.Term = term;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<GroupDto>.Ok(GroupDto.From(group, await groups.CountMembersAsync(id, ct)));
    }

    // Medlemskap och kurskopplingar tas bort med gruppen. Innehåll publicerat till gruppen
    // (nyheter, uppgifter med egen deadline) stoppar borttagningen, så att inget elevarbete försvinner.
    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var group = await GetOwnAsync(id, ct);
        if (group is null)
        {
            return ServiceResult<bool>.NotFound("group not found");
        }

        if (await groups.HasContentAsync(id, null, ct))
        {
            return ServiceResult<bool>.Conflict("the group has published content");
        }

        await groups.RemoveAsync(group, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    // Tom lista = fristående grupp (t.ex. committee).
    // Null (404) om gruppen inte finns eller är en annan skolas.
    public async Task<List<CourseDto>?> GetCoursesAsync(Guid groupId, CancellationToken ct)
    {
        if (await GetOwnAsync(groupId, ct) is null)
        {
            return null;
        }

        var courses = await groups.GetCoursesAsync(groupId, ct);
        return courses.Select(CourseDto.From).ToList();
    }

    /// <summary>Värdet är false om gruppen redan läser kursen.</summary>
    public async Task<ServiceResult<bool>> AddCourseAsync(Guid groupId, Guid courseId, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        if (await GetOwnAsync(groupId, ct) is null)
        {
            return ServiceResult<bool>.NotFound("group not found");
        }

        var course = await courses.GetAsync(courseId, ct);
        if (course is null || !await currentSchool.OwnsAsync(course.SchoolId, ct))
        {
            return ServiceResult<bool>.NotFound("course not found");
        }

        if (await groups.ReadsCourseAsync(groupId, courseId, ct))
        {
            return ServiceResult<bool>.Ok(false);
        }

        groups.AddCourse(new GroupCourse { GroupId = groupId, CourseId = courseId });
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    // Innehåll publicerat till just gruppen i kursen stoppar, kursens gemensamma innehåll gör det inte.
    public async Task<ServiceResult<bool>> RemoveCourseAsync(Guid groupId, Guid courseId, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        if (await GetOwnAsync(groupId, ct) is null)
        {
            return ServiceResult<bool>.NotFound("group not found");
        }

        if (!await groups.ReadsCourseAsync(groupId, courseId, ct))
        {
            return ServiceResult<bool>.NotFound("the group does not read the course");
        }

        if (await groups.HasContentAsync(groupId, courseId, ct))
        {
            return ServiceResult<bool>.Conflict("the course has content published to the group");
        }

        await groups.RemoveCourseAsync(groupId, courseId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    // En grupp i en annan skola svarar som om den inte fanns.
    public async Task<ServiceResult<List<GroupMemberDto>>> GetMembersAsync(Guid groupId, CancellationToken ct)
    {
        if (await GetOwnAsync(groupId, ct) is null)
        {
            return ServiceResult<List<GroupMemberDto>>.NotFound("group not found");
        }

        return ServiceResult<List<GroupMemberDto>>.Ok(await groups.GetMembersAsync(groupId, ct));
    }

    // Rollen gäller bara i den här gruppen. En lärare kan vara elev i en annan grupp.
    public async Task<ServiceResult<MemberDto>> AddMemberAsync(Guid groupId, AddMemberDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<MemberDto>.Forbidden();
        }

        var group = await GetOwnAsync(groupId, ct);
        if (group is null)
        {
            return ServiceResult<MemberDto>.NotFound("group not found");
        }

        var user = await users.GetAsync(dto.UserId, ct);
        if (user is null)
        {
            return ServiceResult<MemberDto>.Invalid("user does not exist");
        }

        if (user.SchoolId != group.SchoolId)
        {
            return ServiceResult<MemberDto>.Invalid("user belongs to another school");
        }

        if (await groups.IsMemberAsync(groupId, dto.UserId, ct))
        {
            return ServiceResult<MemberDto>.Invalid("user is already in the group");
        }

        var enrollment = new Enrollment { GroupId = groupId, UserId = dto.UserId, Role = dto.Role };
        groups.AddMember(enrollment);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<MemberDto>.Ok(MemberDto.From(enrollment));
    }

    public async Task<ServiceResult<MemberDto>> UpdateMemberAsync(
        Guid groupId, Guid userId, UpdateMemberDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<MemberDto>.Forbidden();
        }

        var enrollment = await GetOwnMemberAsync(groupId, userId, ct);
        if (enrollment is null)
        {
            return ServiceResult<MemberDto>.NotFound("member not found");
        }

        enrollment.Role = dto.Role;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<MemberDto>.Ok(MemberDto.From(enrollment));
    }

    // Elevens inlämningar ligger kvar, de hör till användaren och inte till medlemskapet.
    public async Task<ServiceResult<bool>> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var enrollment = await GetOwnMemberAsync(groupId, userId, ct);
        if (enrollment is null)
        {
            return ServiceResult<bool>.NotFound("member not found");
        }

        groups.RemoveMember(enrollment);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    private async Task<Enrollment?> GetOwnMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        await GetOwnAsync(groupId, ct) is null
            ? null
            : await groups.GetMemberAsync(groupId, userId, ct);

    // En grupp i en annan skola räknas som att den inte finns.
    private async Task<Group?> GetOwnAsync(Guid id, CancellationToken ct)
    {
        var group = await groups.GetAsync(id, ct);
        return group is not null && await currentSchool.OwnsAsync(group.SchoolId, ct)
            ? group
            : null;
    }
}
