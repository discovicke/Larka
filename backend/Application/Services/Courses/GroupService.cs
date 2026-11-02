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

        // Admin ser hela skolan, alla andra grupperna de är medlemmar i och grupperna som läser en kurs de är lärare i.
        var schoolGroups = await permissions.IsAdminAsync(ct)
            ? await groups.GetForSchoolAsync(schoolId.Value, ct)
            : await groups.GetForMemberAsync(schoolId.Value, currentUser.UserId, ct);
        var counts = await groups.GetMemberCountsAsync(schoolId.Value, ct);
        var lastEnds = await groups.GetLastCourseEndsAsync(schoolId.Value, ct);
        return schoolGroups
            .Select(group => GroupDto.From(
                group,
                counts.GetValueOrDefault(group.Id),
                lastEnds.TryGetValue(group.Id, out var endsOn)
                    ? endsOn
                    : null))
            .ToList();
    }

    public async Task<GroupDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var group = await GetOwnAsync(id, ct);
        return group is null
            ? null
            : GroupDto.From(group, await groups.CountMembersAsync(id, ct), null);
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
        return ServiceResult<GroupDto>.Ok(GroupDto.From(group, 0, null));
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
        return ServiceResult<GroupDto>.Ok(GroupDto.From(group, await groups.CountMembersAsync(id, ct), null));
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

    /// <summary>
    /// Lärarna i alla kurser som gruppen läser, i en fråga i stället för en per kurs. Bara admin.
    /// SharedWith är de andra grupperna som läser samma kurs.
    /// </summary>
    public async Task<ServiceResult<List<GroupCourseTeachersDto>>> GetCourseTeachersAsync(
        Guid groupId, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<List<GroupCourseTeachersDto>>.Forbidden();
        }

        if (await GetOwnAsync(groupId, ct) is null)
        {
            return ServiceResult<List<GroupCourseTeachersDto>>.NotFound("group not found");
        }

        var courseIds = (await groups.GetCoursesAsync(groupId, ct)).Select(course => course.Id).ToList();
        var teachers = await courses.GetTeachersForCoursesAsync(courseIds, ct);
        var shared = await groups.GetOtherGroupNamesReadingCoursesAsync(courseIds, groupId, ct);
        return ServiceResult<List<GroupCourseTeachersDto>>.Ok(courseIds
            .Select(courseId => new GroupCourseTeachersDto(
                courseId,
                teachers.GetValueOrDefault(courseId, [])
                    .Select(teacher => new CourseTeacherDto(teacher.UserId, teacher.Name, teacher.Email))
                    .ToList(),
                shared.GetValueOrDefault(courseId, [])))
            .ToList());
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

    // En grupp i en annan skola svarar som om den inte fanns. Listan (bara namn och roll i gruppen) får läsas
    // av gruppens medlemmar, vilken roll de än har i den, av lärarna i en kurs som gruppen läser (de behöver
    // inte vara med i klassen) och av admin.
    public async Task<ServiceResult<List<GroupMemberDto>>> GetMembersAsync(Guid groupId, CancellationToken ct)
    {
        if (await GetOwnAsync(groupId, ct) is null)
        {
            return ServiceResult<List<GroupMemberDto>>.NotFound("group not found");
        }

        if (!await permissions.IsAdminAsync(ct)
            && !await groups.IsMemberAsync(groupId, currentUser.UserId, ct)
            && !await groups.TeachesCourseOfGroupAsync(groupId, currentUser.UserId, ct))
        {
            return ServiceResult<List<GroupMemberDto>>.Forbidden();
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

        var roleError = RoleError(user, dto.Role);
        if (roleError is not null)
        {
            return ServiceResult<MemberDto>.Invalid(roleError);
        }

        // Klassen har en mentor. En ny mentor ersätter den förra, som i Byt mentor på klassens sida.
        if (dto.Role == GroupRole.Mentor)
        {
            await RemoveOtherMentorsAsync(groupId, dto.UserId, ct);
        }

        var enrollment = new Enrollment { GroupId = groupId, UserId = dto.UserId, Role = dto.Role };
        groups.AddMember(enrollment);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<MemberDto>.Ok(MemberDto.From(enrollment));
    }

    private const int _maxImportLines = 500;

    // Klistrad elevlista: nya elever skapas utan lösenord och alla läggs i klassen som elever i ett enda
    // SaveChanges, så att antingen allt sparas eller inget. Rader som inte går att läsa hoppas över och redovisas.
    public async Task<ServiceResult<ImportStudentsResultDto>> ImportStudentsAsync(
        Guid groupId, ImportStudentsDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<ImportStudentsResultDto>.Forbidden();
        }

        var group = await GetOwnAsync(groupId, ct);
        if (group is null)
        {
            return ServiceResult<ImportStudentsResultDto>.NotFound("group not found");
        }

        if (dto.Lines is null || dto.Lines.Count == 0 || dto.Lines.Count > _maxImportLines)
        {
            return ServiceResult<ImportStudentsResultDto>.Invalid(
                $"lines must contain between 1 and {_maxImportLines} students");
        }

        var results = new List<ImportStudentResultDto>();
        var added = new List<GroupMemberDto>();
        var seen = new HashSet<string>();
        foreach (var line in dto.Lines)
        {
            var name = line.Name?.Trim() ?? "";
            var email = line.Email?.Trim().ToLowerInvariant() ?? "";
            var outcome = await ImportLineAsync(group, name, email, seen, added, ct);
            results.Add(new ImportStudentResultDto(name, email, outcome));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ImportStudentsResultDto>.Ok(new ImportStudentsResultDto(results, added));
    }

    private async Task<ImportStudentOutcome> ImportLineAsync(
        Group group, string name, string email, HashSet<string> seen, List<GroupMemberDto> added, CancellationToken ct)
    {
        if (!EmailRule.IsValid(email))
        {
            return ImportStudentOutcome.Invalid;
        }

        if (!seen.Add(email))
        {
            return ImportStudentOutcome.Duplicate;
        }

        var user = await users.GetByEmailAsync(email, ct);
        var created = user is null;
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ImportStudentOutcome.Invalid;
            }

            user = new User { SchoolId = group.SchoolId, Name = name, Email = email, Role = UserRole.Student };
            users.Add(user);
        }
        else if (user.SchoolId != group.SchoolId)
        {
            return ImportStudentOutcome.Invalid;
        }
        else if (user.Role != UserRole.Student)
        {
            return ImportStudentOutcome.NotStudent;
        }
        else if (await groups.IsMemberAsync(group.Id, user.Id, ct))
        {
            return ImportStudentOutcome.AlreadyMember;
        }

        groups.AddMember(new Enrollment { GroupId = group.Id, UserId = user.Id, Role = GroupRole.Student });
        added.Add(new GroupMemberDto(user.Id, user.Name, GroupRole.Student));
        return created
            ? ImportStudentOutcome.Created
            : ImportStudentOutcome.Added;
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

        var member = await users.GetAsync(userId, ct);
        var roleError = member is null
            ? null
            : RoleError(member, dto.Role);
        if (roleError is not null)
        {
            return ServiceResult<MemberDto>.Invalid(roleError);
        }

        if (dto.Role == GroupRole.Mentor)
        {
            await RemoveOtherMentorsAsync(groupId, userId, ct);
        }

        enrollment.Role = dto.Role;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<MemberDto>.Ok(MemberDto.From(enrollment));
    }

    // Bara en lärare i skolan kan vara mentor eller lärare i en klass. Admin och elever kan det inte.
    private static string? RoleError(User user, GroupRole role) =>
        role != GroupRole.Student && user.Role != UserRole.Teacher
            ? "only a teacher can be a mentor or a teacher in a group"
            : null;

    private async Task RemoveOtherMentorsAsync(Guid groupId, Guid keepUserId, CancellationToken ct)
    {
        foreach (var mentor in await groups.GetMentorEnrollmentsAsync(groupId, ct))
        {
            if (mentor.UserId != keepUserId)
            {
                groups.RemoveMember(mentor);
            }
        }
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
