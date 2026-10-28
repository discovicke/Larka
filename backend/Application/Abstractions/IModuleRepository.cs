using Backend.Application.Dtos;
using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IModuleRepository
{
    Task<Module?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Kursens moduler sorterade på Position.</summary>
    Task<List<Module>> GetForCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>
    /// Innehållet i kursens moduler, per modul-id, sorterat på när det publiceras. Med includeSubmissionCounts
    /// fylls inlämningsantalen i för delar med inlämningssätt (en gruppad fråga för hela kursen).
    /// </summary>
    Task<ILookup<Guid, ModuleContentItem>> GetContentByModuleAsync(
        Guid courseId, bool includeSubmissionCounts, CancellationToken ct);

    void Add(Module module);
    void Remove(Module module);
}
