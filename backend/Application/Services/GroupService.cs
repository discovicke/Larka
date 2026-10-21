using Backend.Application.Abstractions;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class GroupService
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _unitOfWork;

    public GroupService(IGroupRepository groups, IUnitOfWork unitOfWork)
    {
        _groups = groups;
        _unitOfWork = unitOfWork;
    }

    public Task<List<Group>> GetAllAsync(CancellationToken ct) => _groups.GetAllAsync(ct);

    // Tom lista = fristående grupp (t.ex. committee).
    public Task<List<Course>> GetCoursesAsync(Guid groupId, CancellationToken ct) =>
        _groups.GetCoursesAsync(groupId, ct);

    /// <summary>Returnerar false om gruppen redan läser kursen.</summary>
    public async Task<bool> AddCourseAsync(Guid groupId, Guid courseId, CancellationToken ct)
    {
        if (await _groups.ReadsCourseAsync(groupId, courseId, ct))
        {
            return false;
        }

        _groups.AddCourse(new GroupCourse { GroupId = groupId, CourseId = courseId });
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
