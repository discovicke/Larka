using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class PartCompletionRepository(AppDb db) : IPartCompletionRepository
{
    public Task<PartCompletion?> GetAsync(Guid linkId, Guid studentId, CancellationToken ct) =>
        db.PartCompletions.FirstOrDefaultAsync(
            completion => completion.ContentLinkId == linkId && completion.StudentId == studentId, ct);

    public Task<List<PartCompletion>> GetForStudentAsync(
        Guid studentId, IReadOnlyCollection<Guid> linkIds, CancellationToken ct) =>
        db.PartCompletions
            .Where(completion => completion.StudentId == studentId && linkIds.Contains(completion.ContentLinkId))
            .ToListAsync(ct);

    public void Add(PartCompletion completion) => db.PartCompletions.Add(completion);

    public void Remove(PartCompletion completion) => db.PartCompletions.Remove(completion);
}
