using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ISchoolGitHubInstallationRepository
{
    Task<SchoolGitHubInstallation?> GetForSchoolAsync(Guid schoolId, CancellationToken ct);
    void Add(SchoolGitHubInstallation installation);
}
