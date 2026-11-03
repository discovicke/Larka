using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Adminens vy av en elev i klassen: läget i alla klassens kurser, samlat. Bara admin i skolan får läsa den. Alla andra, också lärare i en av klassens kurser, svarar som om eleven inte fanns.
public class StudentOverviewService(
    IGroupRepository groups,
    IUserRepository users,
    StudyPlanService studyPlan,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    TimeProvider time)
{
    public async Task<ServiceResult<StudentOverviewDto>> GetAsync(
        Guid groupId, Guid studentId, CancellationToken ct)
    {
        var group = await groups.GetAsync(groupId, ct);
        if (group is null || !await currentSchool.OwnsAsync(group.SchoolId, ct))
        {
            return ServiceResult<StudentOverviewDto>.NotFound("group not found");
        }

        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<StudentOverviewDto>.NotFound("group not found");
        }

        var student = await groups.GetMemberAsync(groupId, studentId, ct);
        var user = student?.Role == GroupRole.Student
            ? await users.GetAsync(studentId, ct)
            : null;
        if (user is null)
        {
            return ServiceResult<StudentOverviewDto>.NotFound("member not found");
        }

        var now = time.GetUtcNow();
        var plan = await studyPlan.GetForStudentAsync(studentId, groupId, ct);
        var courses = plan.Courses
            .Select(course => ToCourse(course, now))
            .ToList();
        return ServiceResult<StudentOverviewDto>.Ok(new StudentOverviewDto(studentId, user.Name, groupId, courses));
    }

    private static StudentOverviewCourseDto ToCourse(StudyPlanCourseDto course, DateTimeOffset now)
    {
        var assignments = course.Assignments;
        int Count(SubmissionStatus status) => assignments.Count(assignment => assignment.Status == status);
        int Graded(GradeLevel level) => assignments.Count(assignment =>
            assignment.Status == SubmissionStatus.Graded && assignment.Grade == level);

        return new StudentOverviewCourseDto(
            course.CourseId,
            course.Title,
            course.Code,
            assignments.Count,
            Count(SubmissionStatus.NeedsWork),
            assignments.Count(assignment =>
                assignment.Status == SubmissionStatus.NotStarted && assignment.DueAt < now),
            Count(SubmissionStatus.Submitted),
            Count(SubmissionStatus.Graded),
            Graded(GradeLevel.Fail),
            Graded(GradeLevel.Pass),
            Graded(GradeLevel.Distinction));
    }
}
