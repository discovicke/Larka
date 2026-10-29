using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ISchoolGitHubInstallationRepository
{
    Task<SchoolGitHubInstallation?> GetForSchoolAsync(Guid schoolId, CancellationToken ct);
    Task<SchoolGitHubInstallation?> GetByInstallationAsync(long installationId, CancellationToken ct);
    void Add(SchoolGitHubInstallation installation);
}
