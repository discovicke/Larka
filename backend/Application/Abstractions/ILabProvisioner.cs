using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

/// <summary>En elev som ska få ett repo, och inlämningen repot hör till.</summary>
public record RepoRequest(Guid SubmissionId, User Student);

public interface ILabProvisioner
{
    // Skapar ett repo per elev från mallen (templateRepo = "org/mall").
    Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
        string templateRepo, string assignmentTitle, IReadOnlyList<RepoRequest> requests, CancellationToken ct);

    Task RefreshCheckStatusAsync(IReadOnlyList<StudentRepo> repos, CancellationToken ct); // pollar checks -> Green/Red
}
