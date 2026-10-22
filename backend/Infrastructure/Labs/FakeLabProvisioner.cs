using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Infrastructure.Labs;

// Fake nu: skapar repo-namn + Pending. Byt till Octokit-implementation senare.
public class FakeLabProvisioner : ILabProvisioner
{
    private readonly string _org;

    public FakeLabProvisioner(IConfiguration cfg) => _org = cfg["GitHub:Org"] ?? "skol-org";

    public Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
        string templateRepo, string assignmentTitle, IReadOnlyList<RepoRequest> requests, CancellationToken ct)
    {
        var repos = requests.Select(r =>
        {
            var slug = r.Student.Email.Split('@')[0].Replace(".", "-").ToLowerInvariant();
            return new StudentRepo
            {
                SubmissionId = r.SubmissionId,
                RepoFullName = $"{_org}/{assignmentTitle.ToLowerInvariant().Replace(" ", "-")}-{slug}",
                CheckStatus = CheckStatus.Pending
            };
        }).ToList();

        return Task.FromResult<IReadOnlyList<StudentRepo>>(repos);
    }

    public Task RefreshCheckStatusAsync(IReadOnlyList<StudentRepo> repos, CancellationToken ct) =>
        Task.CompletedTask;
}
