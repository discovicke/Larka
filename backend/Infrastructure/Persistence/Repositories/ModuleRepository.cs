using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class ModuleRepository(AppDb db) : IModuleRepository
{
    public Task<Module?> GetAsync(Guid id, CancellationToken ct) =>
        db.Modules.FirstOrDefaultAsync(module => module.Id == id, ct);

    public Task<List<Module>> GetForCourseAsync(Guid courseId, CancellationToken ct) =>
        db.Modules
            .Where(module => module.CourseId == courseId)
            .OrderBy(module => module.Position)
            .ToListAsync(ct);

    public async Task<ILookup<Guid, ModuleContentItem>> GetContentByModuleAsync(
        Guid courseId, CancellationToken ct)
    {
        var rows = await (from link in db.ContentLinks
                          join content in db.Content on link.ContentId equals content.Id
                          join spec in db.AssignmentSpecs on content.Id equals spec.ContentId into specs
                          from spec in specs.DefaultIfEmpty()
                          where link.CourseId == courseId && link.ModuleId != null
                          orderby link.Position, link.PublishAt, content.Title
                          select new
                          {
                              ModuleId = link.ModuleId!.Value,
                              Item = new ModuleContentItem(
                                  link.Id, content.Id, content.Type, (GradeScale?)spec!.GradeScale, content.Title,
                                  content.Status, link.GroupId, link.PublishAt, link.DueAt, link.IsPinned)
                          })
            .ToListAsync(ct);
        return rows.ToLookup(row => row.ModuleId, row => row.Item);
    }

    public void Add(Module module) => db.Modules.Add(module);
}
