using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class CourseRepository(AppDb db) : ICourseRepository
{
    public Task<Course?> GetAsync(Guid id, CancellationToken ct) =>
        db.Courses.FirstOrDefaultAsync(course => course.Id == id, ct);

    public Task<List<Course>> GetForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        db.Courses.Where(course => course.SchoolId == schoolId).OrderBy(course => course.Code).ToListAsync(ct);

    // Lärarrollen i en klass ger ingen kurs. Kurserna kommer från CourseTeachers och från elevrollen.
    public Task<List<Course>> GetForMemberAsync(Guid schoolId, Guid userId, CancellationToken ct) =>
        db.Courses
            .Where(course => course.SchoolId == schoolId
                && (db.GroupCourses.Any(groupCourse => groupCourse.CourseId == course.Id
                        && db.Enrollments.Any(enrollment =>
                            enrollment.GroupId == groupCourse.GroupId && enrollment.UserId == userId
                            && enrollment.Role != GroupRole.Teacher))
                    || db.CourseTeachers.Any(courseTeacher =>
                        courseTeacher.CourseId == course.Id && courseTeacher.UserId == userId)))
            .OrderBy(course => course.Code)
            .ToListAsync(ct);

    public async Task<HashSet<Guid>> GetCourseIdsWithTeachersAsync(Guid schoolId, CancellationToken ct) =>
        (await db.CourseTeachers
                .Where(courseTeacher => db.Courses.Any(course =>
                    course.Id == courseTeacher.CourseId && course.SchoolId == schoolId))
                .Select(courseTeacher => courseTeacher.CourseId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

    public Task<List<Course>> GetByIdsAsync(IReadOnlyCollection<Guid> courseIds, CancellationToken ct) =>
        db.Courses.Where(course => courseIds.Contains(course.Id)).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(Guid schoolId, string code, CancellationToken ct) =>
        db.Courses.AnyAsync(course => course.SchoolId == schoolId && course.Code == code, ct);

    public Task<List<CourseTeacherInfo>> GetTeachersAsync(Guid courseId, CancellationToken ct) =>
        db.CourseTeachers
            .Where(courseTeacher => courseTeacher.CourseId == courseId)
            .Join(
                db.Users,
                courseTeacher => courseTeacher.UserId,
                user => user.Id,
                (_, user) => user)
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Email)
            .Select(user => new CourseTeacherInfo(user.Id, user.Name, user.Email, user.AvatarKey))
            .ToListAsync(ct);

    public async Task<Dictionary<Guid, List<CourseTeacherInfo>>> GetTeachersForCoursesAsync(
        IReadOnlyCollection<Guid> courseIds, CancellationToken ct)
    {
        var rows = await db.CourseTeachers
            .Where(courseTeacher => courseIds.Contains(courseTeacher.CourseId))
            .Join(
                db.Users,
                courseTeacher => courseTeacher.UserId,
                user => user.Id,
                (courseTeacher, user) => new { courseTeacher.CourseId, user })
            .OrderBy(row => row.user.Name)
            .ThenBy(row => row.user.Email)
            .Select(row => new
            {
                row.CourseId,
                Info = new CourseTeacherInfo(row.user.Id, row.user.Name, row.user.Email)
            })
            .ToListAsync(ct);
        return rows
            .GroupBy(row => row.CourseId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(row => row.Info).ToList());
    }

    public Task<CourseTeacher?> GetTeacherAsync(Guid courseId, Guid userId, CancellationToken ct) =>
        db.CourseTeachers.FirstOrDefaultAsync(
            courseTeacher => courseTeacher.CourseId == courseId && courseTeacher.UserId == userId, ct);

    public void AddTeacher(CourseTeacher courseTeacher) => db.CourseTeachers.Add(courseTeacher);

    public void RemoveTeacher(CourseTeacher courseTeacher) => db.CourseTeachers.Remove(courseTeacher);

    public async Task RemoveTeachersAsync(Guid courseId, CancellationToken ct) =>
        db.CourseTeachers.RemoveRange(
            await db.CourseTeachers.Where(courseTeacher => courseTeacher.CourseId == courseId).ToListAsync(ct));

    public void Add(Course course) => db.Courses.Add(course);

    public void Remove(Course course) => db.Courses.Remove(course);
}
