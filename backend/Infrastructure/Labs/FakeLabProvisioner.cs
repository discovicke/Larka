using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Infrastructure.Labs;

// Fake nu: skapar repo-namn + Pending. Byt till Octokit-implementation senare.
// Fake simulerar gamla Classroom-känslan: lagrar UpstreamUrl per repo + "git remote add upstream".
public class FakeLabProvisioner : ILabProvisioner
{
    private readonly string _org;

    public FakeLabProvisioner(IConfiguration cfg) => _org = cfg["GitHub:Org"] ?? "skol-org";

    public Task<IReadOnlyList<StudentLabRepo>> ProvisionAsync(
        LabAssignment lab, IEnumerable<User> students, CancellationToken ct)
    {
        var repos = students.Select(s =>
        {
            var slug = s.Email.Split('@')[0].Replace(".", "-").ToLowerInvariant();
            return new StudentLabRepo
            {
                LabId = lab.Id,
                StudentId = s.Id,
                RepoFullName = $"{_org}/{lab.Title.ToLowerInvariant().Replace(" ", "-")}-{slug}",
                CheckStatus = CheckStatus.Pending
            };
        }).ToList();

        return Task.FromResult<IReadOnlyList<StudentLabRepo>>(repos);
    }

    public Task RefreshCheckStatusAsync(IReadOnlyList<StudentLabRepo> repos, CancellationToken ct) =>
        Task.CompletedTask;
}
