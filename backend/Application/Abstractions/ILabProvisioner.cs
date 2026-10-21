using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ILabProvisioner
{
    Task<IReadOnlyList<StudentLabRepo>> ProvisionAsync(LabAssignment lab, IEnumerable<User> students, CancellationToken ct);
    Task RefreshCheckStatusAsync(IReadOnlyList<StudentLabRepo> repos, CancellationToken ct); // pollar checks -> Green/Red
}
