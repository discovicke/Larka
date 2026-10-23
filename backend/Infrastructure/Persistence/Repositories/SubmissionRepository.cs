using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class SubmissionRepository(AppDb db) : ISubmissionRepository
{
    public Task<Submission?> GetAsync(Guid id, CancellationToken ct) =>
        WithDetails().FirstOrDefaultAsync(submission => submission.Id == id, ct);

    public Task<Submission?> GetForStudentAsync(Guid linkId, Guid studentId, CancellationToken ct) =>
        WithDetails().FirstOrDefaultAsync(
            submission => submission.ContentLinkId == linkId && submission.StudentId == studentId, ct);

    public Task<List<Submission>> GetByLinkAsync(Guid linkId, CancellationToken ct) =>
        WithDetails().Where(submission => submission.ContentLinkId == linkId).ToListAsync(ct);

    public void Add(Submission submission) => db.Submissions.Add(submission);

    public void AddRange(IEnumerable<Submission> submissions) => db.Submissions.AddRange(submissions);

    public void AddFiles(IEnumerable<SubmissionFile> files) => db.SubmissionFiles.AddRange(files);

    public void RemoveFiles(IEnumerable<SubmissionFile> files) => db.SubmissionFiles.RemoveRange(files);

    public void AddGrade(Grade grade) => db.Grades.Add(grade);

    public Task<List<StudentRepo>> GetAllReposAsync(CancellationToken ct) => db.StudentRepos.ToListAsync(ct);

    private IQueryable<Submission> WithDetails() =>
        db.Submissions
            .Include(submission => submission.Files)
            .Include(submission => submission.Repo)
            .Include(submission => submission.Grade);
}
