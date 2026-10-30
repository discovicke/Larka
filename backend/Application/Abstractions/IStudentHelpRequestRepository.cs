using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IStudentHelpRequestRepository
{
    Task<StudentHelpRequest?> GetAsync(Guid contentLinkId, Guid studentId, CancellationToken ct);

    Task<List<StudentHelpRequest>> GetForLinksAsync(
        IReadOnlyCollection<Guid> contentLinkIds, CancellationToken ct);

    void Add(StudentHelpRequest request);
}
