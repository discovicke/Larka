using Backend.Application.Abstractions;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class SubmissionRepository(AppDb db) : ISubmissionRepository
{
    public Task<Submission?> GetAsync(Guid id, CancellationToken ct) =>
        WithDetails().FirstOrDefaultAsync(submission => submission.Id == id, ct);

    public Task<Submission?> GetForStudentAsync(Guid linkId, Guid studentId, CancellationToken ct) =>
        WithDetails().FirstOrDefaultAsync(
            submission => submission.ContentLinkId == linkId && submission.StudentId == studentId, ct);

    public Task<List<Submission>> GetForStudentAsync(
        Guid studentId, IReadOnlyCollection<Guid> linkIds, CancellationToken ct) =>
        WithDetails().Where(submission =>
            submission.StudentId == studentId && linkIds.Contains(submission.ContentLinkId)).ToListAsync(ct);

    public Task<List<Submission>> GetForStudentsAsync(
        IReadOnlyCollection<Guid> studentIds, IReadOnlyCollection<Guid> linkIds, CancellationToken ct) =>
        WithDetails().Where(submission =>
            studentIds.Contains(submission.StudentId) && linkIds.Contains(submission.ContentLinkId)).ToListAsync(ct);

    public Task<List<Submission>> GetRecentForStudentAsync(Guid studentId, int take, CancellationToken ct) =>
        WithDetails()
            .Where(submission => submission.StudentId == studentId
                && submission.Status != SubmissionStatus.NotStarted)
            .OrderByDescending(submission => submission.Grade != null
                ? submission.Grade.GradedAt
                : submission.SubmittedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<List<Submission>> GetByLinkAsync(Guid linkId, CancellationToken ct) =>
        WithDetails().Where(submission => submission.ContentLinkId == linkId).ToListAsync(ct);

    public Task<List<SubmissionToGrade>> GetToGradeAsync(
        IReadOnlyCollection<Guid> courseIds, int take, CancellationToken ct) =>
        (from submission in db.Submissions
         join link in db.ContentLinks on submission.ContentLinkId equals link.Id
         join content in db.Content on link.ContentId equals content.Id
         join student in db.Users on submission.StudentId equals student.Id
         where link.CourseId != null
             && courseIds.Contains(link.CourseId.Value)
             && submission.Status == SubmissionStatus.Submitted
             // Bara inlämningar (uppgifter med betygsskala) väntar på läraren. En övning får feedback när läraren vill.
             && db.AssignmentSpecs.Any(spec => spec.ContentId == content.Id && spec.GradeScale != GradeScale.None)
         orderby submission.SubmittedAt, content.Title, student.Name
         select new SubmissionToGrade(
             submission.Id,
             link.Id,
             content.Title,
             link.CourseId!.Value,
             student.Id,
             student.Name,
             submission.SubmittedAt ?? DateTimeOffset.MinValue))
        .Take(take)
        .ToListAsync(ct);

    public Task<List<CourseToGradeCount>> GetToGradeCountsAsync(
        IReadOnlyCollection<Guid> courseIds, CancellationToken ct) =>
        (from submission in db.Submissions
         join link in db.ContentLinks on submission.ContentLinkId equals link.Id
         where link.CourseId != null
             && courseIds.Contains(link.CourseId.Value)
             && submission.Status == SubmissionStatus.Submitted
             // Bara inlämningar (uppgifter med betygsskala) väntar på läraren. En övning får feedback när läraren vill.
             && db.AssignmentSpecs.Any(spec => spec.ContentId == link.ContentId && spec.GradeScale != GradeScale.None)
         group link by link.CourseId!.Value into courseGroup
         select new CourseToGradeCount(courseGroup.Key, courseGroup.Count()))
        .ToListAsync(ct);

    public async Task<Dictionary<Guid, (int Submitted, int Students)>> GetAudienceCountsAsync(
        IReadOnlyCollection<Guid> linkIds, CancellationToken ct)
    {
        if (linkIds.Count == 0)
        {
            return [];
        }

        var links = await db.ContentLinks.Where(link => linkIds.Contains(link.Id)).ToListAsync(ct);
        return await LinkAudience.CountAsync(db, links, ct);
    }

    public Task<bool> IsTeacherOfLinkAsync(Guid linkId, Guid userId, CancellationToken ct)
    {
        var teacherGroupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role == GroupRole.Teacher)
            .Select(enrollment => enrollment.GroupId);
        var taughtCourseIds = db.CourseTeachers
            .Where(courseTeacher => courseTeacher.UserId == userId)
            .Select(courseTeacher => courseTeacher.CourseId);

        // En länk med kurs avgörs av kursens lärare, även när den också är riktad till en grupp. Bara en länk till
        // en grupp utan kurs avgörs av lärarrollen i gruppen.
        return db.ContentLinks
            .Where(link => link.Id == linkId)
            .AnyAsync(link =>
                link.CourseId != null
                    ? taughtCourseIds.Contains(link.CourseId.Value)
                    : link.GroupId != null && teacherGroupIds.Contains(link.GroupId.Value),
                ct);
    }

    public void Add(Submission submission) => db.Submissions.Add(submission);

    public void AddRange(IEnumerable<Submission> submissions) => db.Submissions.AddRange(submissions);

    public void Remove(Submission submission) => db.Submissions.Remove(submission);

    public void AddFiles(IEnumerable<SubmissionFile> files) => db.SubmissionFiles.AddRange(files);

    public void RemoveFiles(IEnumerable<SubmissionFile> files) => db.SubmissionFiles.RemoveRange(files);

    public void AddGrade(Grade grade) => db.Grades.Add(grade);

    public Task<List<StudentRepoContext>> GetAllReposAsync(CancellationToken ct) =>
        db.StudentRepos
            .Join(
                db.Submissions,
                repo => repo.SubmissionId,
                submission => submission.Id,
                (repo, submission) => new { Repo = repo, submission.StudentId })
            .Join(
                db.Users,
                item => item.StudentId,
                user => user.Id,
                (item, user) => new StudentRepoContext(item.Repo, user.SchoolId))
            .ToListAsync(ct);

    public async Task<List<RepoProvisioningContext>> GetPendingReposAsync(
        DateTimeOffset now, int take, int maxAttempts, CancellationToken ct)
    {
        var pending = await (from repo in db.StudentRepos
                             join submission in db.Submissions on repo.SubmissionId equals submission.Id
                             join student in db.Users on submission.StudentId equals student.Id
                             where (repo.ProvisioningState == RepoProvisioningState.Pending
                                     || repo.ProvisioningState == RepoProvisioningState.Failed)
                                 && repo.ProvisioningAttempts < maxAttempts
                                 && (repo.NextProvisioningAttemptAt == null || repo.NextProvisioningAttemptAt <= now)
                             orderby repo.NextProvisioningAttemptAt, repo.UpdatedAt
                             select new RepoProvisioningContext(repo, student))
            .Take(take)
            .ToListAsync(ct);

        var groupIds = pending
            .Select(context => context.Repo.AssignmentGroupId)
            .Where(groupId => groupId is not null)
            .Select(groupId => groupId!.Value)
            .Distinct()
            .ToList();
        if (groupIds.Count == 0)
        {
            return pending;
        }

        var members = await db.AssignmentGroupMembers
            .Where(member => groupIds.Contains(member.AssignmentGroupId))
            .Include(member => member.User)
            .ToListAsync(ct);
        var byGroup = members
            .Where(member => member.User is not null)
            .GroupBy(member => member.AssignmentGroupId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<User>)group.Select(member => member.User!).ToList());

        return pending
            .Select(context => context.Repo.AssignmentGroupId is not null
                && byGroup.TryGetValue(context.Repo.AssignmentGroupId.Value, out var groupMembers)
                    ? context with { GroupMembers = groupMembers }
                    : context)
            .ToList();
    }

    // Ett repo är öppet när det aldrig låsts eller har öppnats igen efter en komplettering. UpdatedAt är
    // stämpeln för senast behandlade deadline: lås, öppning och varje behandling sätter den, så ett repo
    // är aktuellt först när en deadline som är nyare än stämpeln har passerat. När ingen deadline återstår
    // är varje öppet repo aktuellt, även ett som skapades efter sista deadline. Elevens personliga deadline
    // är en deadline till. Ett grupprepo delas av alla medlemmar och följer den SENASTE personliga deadlinen
    // bland medlemmarnas inlämningar, så att ingen i gruppen stängs ute i förtid.
    public async Task<List<RepoLockContext>> GetReposToLockAsync(
        DateTimeOffset now, int take, CancellationToken ct)
    {
        var rows = await (
            from repo in db.StudentRepos
            join submission in db.Submissions on repo.SubmissionId equals submission.Id
            join student in db.Users on submission.StudentId equals student.Id
            join link in db.ContentLinks on submission.ContentLinkId equals link.Id
            join content in db.Content on link.ContentId equals content.Id
            let personalDueAt = repo.AssignmentGroupId == null
                ? submission.PersonalDueAt
                : db.Submissions
                    .Where(member => member.AssignmentGroupId == repo.AssignmentGroupId
                        && member.ContentLinkId == link.Id)
                    .Max(member => member.PersonalDueAt)
            where repo.ProvisioningState == RepoProvisioningState.Ready
                && (repo.LockedAt == null || repo.ReopenedAt != null)
                && link.DueAt != null
                && ((link.DueAt >= repo.UpdatedAt && link.DueAt <= now)
                    || link.ResubmitDueAt.Any(due => due >= repo.UpdatedAt && due <= now)
                    || (personalDueAt >= repo.UpdatedAt && personalDueAt <= now)
                    || (link.DueAt <= now
                        && !link.ResubmitDueAt.Any(due => due > now)
                        && (personalDueAt == null || personalDueAt <= now)))
                && content.Assignment != null
                && content.Assignment.LockAtDeadline
                && content.Assignment.SubmissionModes.Contains(SubmissionMode.Repo)
            orderby repo.UpdatedAt
            select new
            {
                Repo = repo,
                Submission = submission,
                student.SchoolId,
                DueAt = link.DueAt!.Value,
                link.ResubmitDueAt,
                PersonalDueAt = personalDueAt
            })
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Select(row =>
            {
                var window = new GradingWindow(row.DueAt, row.ResubmitDueAt, now, row.PersonalDueAt);
                return new RepoLockContext(
                    row.Repo,
                    row.Submission,
                    row.SchoolId,
                    window.LastPassedDeadline ?? row.DueAt,
                    row.DueAt,
                    row.ResubmitDueAt,
                    row.PersonalDueAt);
            })
            .ToList();
    }

    public async Task<List<RepoLockContext>> GetRepoContextsForLinkAsync(
        Guid linkId, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await (
            from repo in db.StudentRepos
            join submission in db.Submissions on repo.SubmissionId equals submission.Id
            join student in db.Users on submission.StudentId equals student.Id
            join link in db.ContentLinks on submission.ContentLinkId equals link.Id
            let personalDueAt = repo.AssignmentGroupId == null
                ? submission.PersonalDueAt
                : db.Submissions
                    .Where(member => member.AssignmentGroupId == repo.AssignmentGroupId
                        && member.ContentLinkId == link.Id)
                    .Max(member => member.PersonalDueAt)
            where submission.ContentLinkId == linkId
            orderby student.Name
            select new
            {
                Repo = repo,
                Submission = submission,
                student.SchoolId,
                link.DueAt,
                link.ResubmitDueAt,
                PersonalDueAt = personalDueAt
            })
            .ToListAsync(ct);

        return rows
            .Select(row =>
            {
                var window = new GradingWindow(row.DueAt, row.ResubmitDueAt, now, row.PersonalDueAt);
                return new RepoLockContext(
                    row.Repo,
                    row.Submission,
                    row.SchoolId,
                    window.LastPassedDeadline ?? row.DueAt ?? now,
                    row.DueAt,
                    row.ResubmitDueAt,
                    row.PersonalDueAt);
            })
            .ToList();
    }

    public Task<StudentRepoContext?> GetRepoContextByFullNameAsync(
        string repoFullName, CancellationToken ct) =>
        (from repo in db.StudentRepos
         join submission in db.Submissions on repo.SubmissionId equals submission.Id
         join student in db.Users on submission.StudentId equals student.Id
         where repo.RepoFullName == repoFullName
         select new StudentRepoContext(repo, student.SchoolId))
        .FirstOrDefaultAsync(ct);

    public async Task ReplaceCheckRunsAsync(
        Guid submissionId, IReadOnlyList<StudentRepoCheckRun> runs, CancellationToken ct)
    {
        var existing = await db.StudentRepoCheckRuns
            .Where(checkRun => checkRun.SubmissionId == submissionId)
            .ToListAsync(ct);
        db.StudentRepoCheckRuns.RemoveRange(existing);
        db.StudentRepoCheckRuns.AddRange(runs);
    }

    public async Task<List<StudentRepo>> GetReposForStudentAsync(Guid studentId, CancellationToken ct)
    {
        var individual = from repo in db.StudentRepos
                         join submission in db.Submissions on repo.SubmissionId equals submission.Id
                         where submission.StudentId == studentId
                         select repo;
        var shared = from repo in db.StudentRepos
                     join assignmentGroup in db.AssignmentGroups
                         on repo.AssignmentGroupId equals assignmentGroup.Id
                     join member in db.AssignmentGroupMembers
                         on assignmentGroup.Id equals member.AssignmentGroupId
                     where member.StudentId == studentId
                     select repo;
        return await individual.Concat(shared).Distinct().ToListAsync(ct);
    }

    public Task<List<Submission>> GetSubmissionsForGroupAsync(Guid groupId, CancellationToken ct) =>
        db.Submissions
            .Where(submission => submission.AssignmentGroupId == groupId)
            .ToListAsync(ct);

    public async Task<bool> ReassignGroupRepoAsync(
        Guid groupId, Guid newSubmissionId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.StudentRepos
            .Include(repo => repo.CheckRuns)
            .FirstOrDefaultAsync(repo => repo.AssignmentGroupId == groupId, ct);
        if (existing is null || existing.SubmissionId == newSubmissionId)
        {
            await transaction.CommitAsync(ct);
            return existing is not null;
        }

        var checkRuns = existing.CheckRuns.ToList();
        existing.AssignmentGroupId = null;
        await db.SaveChangesAsync(ct);

        var replacement = new StudentRepo { SubmissionId = newSubmissionId };
        db.Entry(replacement).CurrentValues.SetValues(existing);
        replacement.SubmissionId = newSubmissionId;
        replacement.AssignmentGroupId = groupId;
        db.StudentRepos.Remove(existing);
        db.StudentRepos.Add(replacement);
        foreach (var checkRun in checkRuns)
        {
            db.StudentRepoCheckRuns.Add(new StudentRepoCheckRun
            {
                SubmissionId = newSubmissionId,
                ExternalKey = checkRun.ExternalKey,
                Name = checkRun.Name,
                Status = checkRun.Status,
                Conclusion = checkRun.Conclusion,
                UpdatedAt = checkRun.UpdatedAt,
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public Task<int> CountReposForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        (from repo in db.StudentRepos
         join submission in db.Submissions on repo.SubmissionId equals submission.Id
         join student in db.Users on submission.StudentId equals student.Id
         where student.SchoolId == schoolId
         select repo).CountAsync(ct);

    private IQueryable<Submission> WithDetails() =>
        db.Submissions
            .Include(submission => submission.Files)
            .Include(submission => submission.Repo)
                .ThenInclude(repo => repo!.CheckRuns)
            .Include(submission => submission.Group)
                .ThenInclude(group => group!.Members)
                .ThenInclude(member => member.User)
            .Include(submission => submission.Group)
                .ThenInclude(group => group!.Repo)
                .ThenInclude(repo => repo!.CheckRuns)
            .Include(submission => submission.Grade);
}
