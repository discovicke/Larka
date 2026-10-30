using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class StudentHelpRequestRepository(AppDb db) : IStudentHelpRequestRepository
{
    public Task<StudentHelpRequest?> GetAsync(
        Guid contentLinkId, Guid studentId, CancellationToken ct) =>
        db.StudentHelpRequests.FirstOrDefaultAsync(
            request => request.ContentLinkId == contentLinkId && request.StudentId == studentId,
            ct);

    public Task<List<StudentHelpRequest>> GetForLinksAsync(
        IReadOnlyCollection<Guid> contentLinkIds, CancellationToken ct) =>
        db.StudentHelpRequests
            .Where(request => contentLinkIds.Contains(request.ContentLinkId))
            .ToListAsync(ct);

    public void Add(StudentHelpRequest request) => db.StudentHelpRequests.Add(request);
}
