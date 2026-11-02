using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

/// <summary>Användarens starkaste roll i en kurs: lärare via kursen, mentor och elev via grupperna som läser den.</summary>
public record CourseRole(Guid CourseId, GroupRole Role);

/// <summary>En kurs användaren läser som elev, och grupperna hen läser den i.</summary>
public record StudentCourse(Course Course, List<Guid> GroupIds);

/// <summary>En elev i en kurs, via grupperna som läser den.</summary>
public record CourseStudent(Guid CourseId, Guid UserId);

/// <summary>En grupp med id och namn.</summary>
public record GroupRef(Guid Id, string Name);

public interface IGroupRepository
{
    Task<List<Group>> GetForSchoolAsync(Guid schoolId, CancellationToken ct);
    /// <summary>
    /// Grupperna användaren är medlem i, med vilken roll som helst, i skolan, och grupperna som läser en kurs
    /// användaren är lärare i.
    /// </summary>
    Task<List<Group>> GetForMemberAsync(Guid schoolId, Guid userId, CancellationToken ct);
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

    /// <summary>Användaren är lärare i kursen (en rad i CourseTeachers).</summary>
    Task<bool> IsTeacherOfCourseAsync(Guid courseId, Guid userId, CancellationToken ct);

    /// <summary>Användaren är lärare i minst en kurs, eller har lärarrollen i minst en grupp.</summary>
    Task<bool> TeachesAnythingAsync(Guid userId, CancellationToken ct);

    /// <summary>Eleverna i grupperna där användaren är mentor.</summary>
    Task<HashSet<Guid>> GetMentoredStudentIdsAsync(Guid userId, CancellationToken ct);

    /// <summary>Mentorerna i gruppen, också spärrade, för att byta mentor.</summary>
    Task<List<Enrollment>> GetMentorEnrollmentsAsync(Guid groupId, CancellationToken ct);

    /// <summary>Medlemskapen i skolans grupper, för adminöversikten.</summary>
    Task<List<Enrollment>> GetEnrollmentsForSchoolAsync(Guid schoolId, CancellationToken ct);

    /// <summary>Vilka kurser skolans grupper läser, för adminöversikten.</summary>
    Task<List<GroupCourse>> GetGroupCoursesForSchoolAsync(Guid schoolId, CancellationToken ct);

    /// <summary>Användaren är lärare i en kurs som gruppen läser.</summary>
    Task<bool> TeachesCourseOfGroupAsync(Guid groupId, Guid userId, CancellationToken ct);

    /// <summary>Namnen på grupperna som läser kursen, sorterade.</summary>
    Task<List<string>> GetGroupNamesReadingCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>Namnen på de andra grupperna som läser varje kurs, per kurs-id.</summary>
    Task<Dictionary<Guid, List<string>>> GetOtherGroupNamesReadingCoursesAsync(
        IReadOnlyCollection<Guid> courseIds, Guid exceptGroupId, CancellationToken ct);

    /// <summary>Användarens starkaste roll per kurs, Teacher före Mentor före Student.</summary>
    Task<List<CourseRole>> GetCourseRolesAsync(Guid userId, CancellationToken ct);

    /// <summary>Grupperna användaren är elev i.</summary>
    Task<List<Guid>> GetStudentGroupIdsAsync(Guid userId, CancellationToken ct);

    /// <summary>Kurserna användaren läser som elev i någon grupp, med grupperna per kurs.</summary>
    Task<List<StudentCourse>> GetStudentCoursesAsync(Guid userId, CancellationToken ct);

    /// <summary>Eleverna i grupperna som läser kurserna, en rad per elev och kurs.</summary>
    Task<List<CourseStudent>> GetStudentsByCourseAsync(
        IReadOnlyCollection<Guid> courseIds, CancellationToken ct);

    /// <summary>Grupperna användaren är medlem i, med vilken roll som helst, sorterade på namn.</summary>
    Task<List<GroupRef>> GetMemberGroupsAsync(Guid userId, CancellationToken ct);

    /// <summary>Medlemmarna i gruppen, med namn och roll. Spärrade användare är inte med.</summary>
    Task<List<GroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct);

    /// <summary>Antal medlemmar per grupp i skolan, i en fråga. Grupper utan medlemmar saknas i svaret.</summary>
    Task<Dictionary<Guid, int>> GetMemberCountsAsync(Guid schoolId, CancellationToken ct);

    Task<int> CountMembersAsync(Guid groupId, CancellationToken ct);

    /// <summary>
    /// Sista slutdagen bland gruppens kurser, per grupp i skolan, i en fråga. Grupper utan kurser, och grupper där
    /// någon kurs saknar slutdatum, saknas i svaret.
    /// </summary>
    Task<Dictionary<Guid, DateOnly>> GetLastCourseEndsAsync(Guid schoolId, CancellationToken ct);

    Task<Enrollment?> GetMemberAsync(Guid groupId, Guid userId, CancellationToken ct);

    /// <summary>Innehåll är publicerat till gruppen. Är courseId satt räknas bara länkar till den kursen.</summary>
    Task<bool> HasContentAsync(Guid groupId, Guid? courseId, CancellationToken ct);

    void Add(Group group);
    void AddCourse(GroupCourse groupCourse);
    void AddMember(Enrollment enrollment);
    void RemoveMember(Enrollment enrollment);
    Task RemoveCourseAsync(Guid groupId, Guid courseId, CancellationToken ct);

    /// <summary>Tar bort gruppen med dess medlemskap och kurskopplingar.</summary>
    Task RemoveAsync(Group group, CancellationToken ct);
}
