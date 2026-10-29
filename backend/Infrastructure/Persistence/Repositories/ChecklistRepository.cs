using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class ChecklistRepository(AppDb db) : IChecklistRepository
{
    public Task<List<ChecklistTick>> GetForStudentAsync(Guid linkId, Guid studentId, CancellationToken ct) =>
        db.ChecklistTicks
            .Where(tick => tick.ContentLinkId == linkId && tick.StudentId == studentId)
            .OrderBy(tick => tick.ItemIndex)
            .ToListAsync(ct);

    public void Add(ChecklistTick tick) => db.ChecklistTicks.Add(tick);

    public void RemoveRange(IEnumerable<ChecklistTick> ticks) => db.ChecklistTicks.RemoveRange(ticks);
}
