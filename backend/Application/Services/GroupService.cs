using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class GroupService(
    IGroupRepository groups,
    ISchoolRepository schools,
    IUserRepository users,
    IUnitOfWork unitOfWork)
{
    public async Task<List<GroupDto>> GetAllAsync(CancellationToken ct)
    {
        var allGroups = await groups.GetAllAsync(ct);
        return allGroups.Select(GroupDto.From).ToList();
    }

    public async Task<GroupDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var group = await groups.GetAsync(id, ct);
        return group is null
            ? null
            : GroupDto.From(group);
    }

    // Typen är en enum i DTO:n, så okända typer stoppas redan när JSON läses in (400).
    public async Task<ValidationResult<GroupDto>> CreateAsync(CreateGroupDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ValidationResult<GroupDto>.Invalid("name is required");
        }

        if (!await schools.ExistsAsync(dto.SchoolId, ct))
        {
            return ValidationResult<GroupDto>.Invalid("school does not exist");
        }

        var name = dto.Name.Trim();
        var term = dto.Term?.Trim() ?? "";
        if (await groups.NameExistsAsync(dto.SchoolId, name, term, ct))
        {
            return ValidationResult<GroupDto>.Invalid("the school already has a group with this name and term");
        }

        var group = new Group { SchoolId = dto.SchoolId, Type = dto.Type, Name = name, Term = term };
        groups.Add(group);
        await unitOfWork.SaveChangesAsync(ct);
        return ValidationResult<GroupDto>.Ok(GroupDto.From(group));
    }

    // Tom lista = fristående grupp (t.ex. committee).
    public async Task<List<CourseDto>> GetCoursesAsync(Guid groupId, CancellationToken ct)
    {
        var courses = await groups.GetCoursesAsync(groupId, ct);
        return courses.Select(CourseDto.From).ToList();
    }

    /// <summary>Värdet är false om gruppen redan läser kursen.</summary>
    public async Task<ServiceResult<bool>> AddCourseAsync(Guid groupId, Guid courseId, CancellationToken ct)
    {
        if (!await groups.ExistsAsync(groupId, ct))
        {
            return ServiceResult<bool>.NotFound("group not found");
        }

        if (!await groups.CourseExistsAsync(courseId, ct))
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

    // Rollen gäller bara i den här gruppen. En lärare kan vara elev i en annan grupp.
    public async Task<ServiceResult<MemberDto>> AddMemberAsync(Guid groupId, AddMemberDto dto, CancellationToken ct)
    {
        var group = await groups.GetAsync(groupId, ct);
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
}
