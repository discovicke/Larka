using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class GroupService(IGroupRepository groups, IUnitOfWork unitOfWork)
{
    public async Task<List<GroupDto>> GetAllAsync(CancellationToken ct)
    {
        var allGroups = await groups.GetAllAsync(ct);
        return allGroups.Select(GroupDto.From).ToList();
    }

    // Tom lista = fristående grupp (t.ex. committee).
    public async Task<List<CourseDto>> GetCoursesAsync(Guid groupId, CancellationToken ct)
    {
        var courses = await groups.GetCoursesAsync(groupId, ct);
        return courses.Select(CourseDto.From).ToList();
    }

    /// <summary>Returnerar false om gruppen redan läser kursen.</summary>
    public async Task<bool> AddCourseAsync(Guid groupId, Guid courseId, CancellationToken ct)
    {
        if (await groups.ReadsCourseAsync(groupId, courseId, ct))
        {
            return false;
        }

        groups.AddCourse(new GroupCourse { GroupId = groupId, CourseId = courseId });
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
