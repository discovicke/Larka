using Backend.Application.Abstractions;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.Repositories;

public class SubmissionRepository : ISubmissionRepository
{
    private readonly AppDb _db;

    public SubmissionRepository(AppDb db) => _db = db;

    public void Add(Submission submission) => _db.Submissions.Add(submission);

    public void AddGrade(Grade grade) => _db.Grades.Add(grade);
}
