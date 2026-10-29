using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IChecklistRepository
{
    /// <summary>Elevens avbockade punkter på delen.</summary>
    Task<List<ChecklistTick>> GetForStudentAsync(Guid linkId, Guid studentId, CancellationToken ct);

    void Add(ChecklistTick tick);
    void RemoveRange(IEnumerable<ChecklistTick> ticks);
}
