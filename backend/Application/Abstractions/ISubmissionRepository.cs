using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ISubmissionRepository
{
    void Add(Submission submission);
    void AddGrade(Grade grade);
}
