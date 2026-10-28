using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

/// <summary>Användarens starkaste roll i en kurs, via grupperna som läser den.</summary>
public record CourseRole(Guid CourseId, GroupRole Role);

/// <summary>En lärare i en grupp: en rad per användare och grupp.</summary>
public record GroupTeacher(Guid UserId, string Name, string Email, string GroupName);

/// <summary>En kurs användaren läser som elev, och grupperna hen läser den i.</summary>
public record StudentCourse(Course Course, List<Guid> GroupIds);

public interface IGroupRepository
{
    Task<List<Group>> GetAllAsync(CancellationToken ct);
    Task<Group?> GetAsync(Guid groupId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid groupId, CancellationToken ct);
    Task<bool> NameExistsAsync(Guid schoolId, string name, string term, CancellationToken ct);
    Task<bool> CourseExistsAsync(Guid courseId, CancellationToken ct);
    Task<List<Course>> GetCoursesAsync(Guid groupId, CancellationToken ct);
    Task<List<Guid>> GetGroupIdsReadingCourseAsync(Guid courseId, CancellationToken ct);
    Task<bool> ReadsCourseAsync(Guid groupId, Guid courseId, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);

    /// <summary>Användaren är lärare i gruppen.</summary>
    Task<bool> IsTeacherAsync(Guid groupId, Guid userId, CancellationToken ct);

    /// <summary>Användaren är lärare i en grupp som läser kursen.</summary>
    Task<bool> IsTeacherOfCourseAsync(Guid courseId, Guid userId, CancellationToken ct);

    /// <summary>Lärarna i grupperna, en rad per lärare och grupp.</summary>
    Task<List<GroupTeacher>> GetTeachersAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct);

    /// <summary>Användarens starkaste roll per kurs, Teacher före Mentor före Student.</summary>
    Task<List<CourseRole>> GetCourseRolesAsync(Guid userId, CancellationToken ct);

    /// <summary>Kurserna användaren läser som elev i någon grupp, med grupperna per kurs.</summary>
    Task<List<StudentCourse>> GetStudentCoursesAsync(Guid userId, CancellationToken ct);
    void Add(Group group);
    void AddCourse(GroupCourse groupCourse);
    void AddMember(Enrollment enrollment);
}
