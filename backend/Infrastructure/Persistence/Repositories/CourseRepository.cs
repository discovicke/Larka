using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class CourseRepository(AppDb db) : ICourseRepository
{
    public Task<Course?> GetAsync(Guid id, CancellationToken ct) =>
        db.Courses.FirstOrDefaultAsync(course => course.Id == id, ct);

    public Task<List<Course>> GetForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        db.Courses.Where(course => course.SchoolId == schoolId).OrderBy(course => course.Code).ToListAsync(ct);

    public Task<List<Course>> GetByIdsAsync(IReadOnlyCollection<Guid> courseIds, CancellationToken ct) =>
        db.Courses.Where(course => courseIds.Contains(course.Id)).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(Guid schoolId, string code, CancellationToken ct) =>
        db.Courses.AnyAsync(course => course.SchoolId == schoolId && course.Code == code, ct);

    public void Add(Course course) => db.Courses.Add(course);
}
