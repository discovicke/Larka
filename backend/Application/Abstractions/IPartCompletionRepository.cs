using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IPartCompletionRepository
{
    Task<PartCompletion?> GetAsync(Guid linkId, Guid studentId, CancellationToken ct);

    /// <summary>Elevens klarmarkeringar på de länkarna.</summary>
    Task<List<PartCompletion>> GetForStudentAsync(
        Guid studentId, IReadOnlyCollection<Guid> linkIds, CancellationToken ct);

    void Add(PartCompletion completion);
    void Remove(PartCompletion completion);
}
