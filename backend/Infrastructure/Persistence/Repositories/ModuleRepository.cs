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
        Guid courseId, bool includeSubmissionCounts, CancellationToken ct)
    {
        var rows = await (from link in db.ContentLinks
                          join content in db.Content on link.ContentId equals content.Id
                          join spec in db.AssignmentSpecs on content.Id equals spec.ContentId into specs
                          from spec in specs.DefaultIfEmpty()
                          where link.CourseId == courseId && link.ModuleId != null
                          orderby link.Position, link.PublishAt, content.Title
                          select new { ModuleId = link.ModuleId!.Value, Link = link, Content = content, Spec = spec })
            .ToListAsync(ct);

        var linkIds = rows.Select(row => row.Link.Id).ToList();
        var dependencies = linkIds.Count == 0
            ? []
            : await db.ContentLinkDependencies
                .Where(dependency => linkIds.Contains(dependency.BlockedLinkId))
                .ToListAsync(ct);
        var activeSubmissionLinkIds = linkIds.Count == 0
            ? []
            : await db.Submissions
                .Where(submission => linkIds.Contains(submission.ContentLinkId)
                    && (submission.Status != SubmissionStatus.NotStarted
                        || submission.SubmittedAt != null
                        || submission.Files.Any()
                        || submission.Repo != null
                        || submission.Grade != null))
                .Select(submission => submission.ContentLinkId)
                .Distinct()
                .ToListAsync(ct);
        var completedLinkIds = linkIds.Count == 0
            ? []
            : await db.PartCompletions
                .Where(completion => linkIds.Contains(completion.ContentLinkId))
                .Select(completion => completion.ContentLinkId)
                .Distinct()
                .ToListAsync(ct);
        var counts = includeSubmissionCounts
            ? await LinkAudience.CountAsync(
                db,
                rows.Where(row => row.Spec is not null && row.Spec.SubmissionModes.Count > 0)
                    .Select(row => row.Link)
                    .ToList(),
                ct)
            : [];
        var hasActivity = activeSubmissionLinkIds.Concat(completedLinkIds).ToHashSet();
        // Veckans ordning, så att "Låst av"-chipsen alltid visas i samma följd som delarna.
        var orderByLinkId = rows
            .Select((row, index) => (LinkId: row.Link.Id, Order: index))
            .ToDictionary(entry => entry.LinkId, entry => entry.Order);
        var blockedBy = dependencies
            .GroupBy(dependency => dependency.BlockedLinkId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(dependency => dependency.BlockingLinkId)
                    .OrderBy(linkId => orderByLinkId.GetValueOrDefault(linkId, int.MaxValue))
                    .ToList());

        return rows
            .Select(row => new
            {
                row.ModuleId,
                Item = new ModuleContentItem(
                    row.Link.Id,
                    row.Content.Id,
                    row.Content.Type,
                    row.Spec is null
                        ? null
                        : row.Spec.GradeScale,
                    row.Spec is null || row.Spec.SubmissionModes.Count == 0
                        ? null
                        : row.Spec.CompletionRule,
                    row.Content.Title,
                    row.Content.Status,
                    row.Link.GroupId,
                    row.Link.PublishAt,
                    row.Link.DueAt,
                    row.Link.ResubmitDueAt,
                    row.Link.IsPinned,
                    blockedBy.GetValueOrDefault(row.Link.Id) ?? [],
                    hasActivity.Contains(row.Link.Id),
                    counts.TryGetValue(row.Link.Id, out var count)
                        ? count.Submitted
                        : null,
                    counts.TryGetValue(row.Link.Id, out count)
                        ? count.Students
                        : null)
            })
            .ToLookup(row => row.ModuleId, row => row.Item);
    }

    public void Add(Module module) => db.Modules.Add(module);

    public void Remove(Module module) => db.Modules.Remove(module);
}
