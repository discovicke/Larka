using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

/// <summary>En lärare i en kurs.</summary>
public record CourseTeacherInfo(Guid UserId, string Name, string Email);

public interface ICourseRepository
{
    Task<Course?> GetAsync(Guid id, CancellationToken ct);
    Task<List<Course>> GetForSchoolAsync(Guid schoolId, CancellationToken ct);
    /// <summary>
    /// Kurserna i skolan som någon av användarens grupper läser, vilken roll användaren än har, och kurserna
    /// användaren är lärare i.
    /// </summary>
    Task<List<Course>> GetForMemberAsync(Guid schoolId, Guid userId, CancellationToken ct);
    Task<List<Course>> GetByIdsAsync(IReadOnlyCollection<Guid> courseIds, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid schoolId, string code, CancellationToken ct);

    /// <summary>Lärarna i kursen, sorterade på namn.</summary>
    Task<List<CourseTeacherInfo>> GetTeachersAsync(Guid courseId, CancellationToken ct);
    Task<CourseTeacher?> GetTeacherAsync(Guid courseId, Guid userId, CancellationToken ct);
    void AddTeacher(CourseTeacher courseTeacher);
    void RemoveTeacher(CourseTeacher courseTeacher);

    /// <summary>Tar bort alla lärarkopplingar till kursen, som måste bort innan kursen kan tas bort.</summary>
    Task RemoveTeachersAsync(Guid courseId, CancellationToken ct);

    void Add(Course course);
    void Remove(Course course);
}
