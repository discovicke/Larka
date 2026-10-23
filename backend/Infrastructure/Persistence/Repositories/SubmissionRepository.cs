using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class SubmissionRepository(AppDb db) : ISubmissionRepository
{
    public Task<Submission?> GetAsync(Guid id, CancellationToken ct) =>
        WithDetails().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Submission?> GetForStudentAsync(Guid linkId, Guid studentId, CancellationToken ct) =>
        WithDetails().FirstOrDefaultAsync(s => s.ContentLinkId == linkId && s.StudentId == studentId, ct);

    public Task<List<Submission>> GetByLinkAsync(Guid linkId, CancellationToken ct) =>
        WithDetails().Where(s => s.ContentLinkId == linkId).ToListAsync(ct);

    public void Add(Submission submission) => db.Submissions.Add(submission);

    public void AddRange(IEnumerable<Submission> submissions) => db.Submissions.AddRange(submissions);

    public void AddFiles(IEnumerable<SubmissionFile> files) => db.SubmissionFiles.AddRange(files);

    public void RemoveFiles(IEnumerable<SubmissionFile> files) => db.SubmissionFiles.RemoveRange(files);

    public void AddGrade(Grade grade) => db.Grades.Add(grade);

    public Task<List<StudentRepo>> GetAllReposAsync(CancellationToken ct) => db.StudentRepos.ToListAsync(ct);

    private IQueryable<Submission> WithDetails() =>
        db.Submissions.Include(s => s.Files).Include(s => s.Repo).Include(s => s.Grade);
}
