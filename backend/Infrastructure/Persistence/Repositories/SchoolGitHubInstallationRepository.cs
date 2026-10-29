using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class SchoolGitHubInstallationRepository(AppDb db) : ISchoolGitHubInstallationRepository
{
    public Task<SchoolGitHubInstallation?> GetForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        db.SchoolGitHubInstallations.FirstOrDefaultAsync(installation => installation.SchoolId == schoolId, ct);

    public Task<SchoolGitHubInstallation?> GetByInstallationAsync(
        long installationId, CancellationToken ct) =>
        db.SchoolGitHubInstallations.FirstOrDefaultAsync(
            installation => installation.InstallationId == installationId, ct);

    public void Add(SchoolGitHubInstallation installation) =>
        db.SchoolGitHubInstallations.Add(installation);
}
