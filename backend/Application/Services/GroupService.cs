using Backend.Application.Abstractions;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class GroupService(IGroupRepository groups, IUnitOfWork unitOfWork)
{
    public Task<List<Group>> GetAllAsync(CancellationToken ct) => groups.GetAllAsync(ct);

    // Tom lista = fristående grupp (t.ex. committee).
    public Task<List<Course>> GetCoursesAsync(Guid groupId, CancellationToken ct) =>
        groups.GetCoursesAsync(groupId, ct);

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
