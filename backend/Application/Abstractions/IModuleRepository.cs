using Backend.Application.Dtos;
using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IModuleRepository
{
    Task<Module?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Kursens moduler sorterade på Position.</summary>
    Task<List<Module>> GetForCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>Innehållet i kursens moduler, per modul-id, sorterat på när det publiceras.</summary>
    Task<ILookup<Guid, ModuleContentItem>> GetContentByModuleAsync(Guid courseId, CancellationToken ct);

    void Add(Module module);
}
