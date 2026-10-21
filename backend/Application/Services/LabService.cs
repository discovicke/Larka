using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

// Labs = Classroom-ersättare (Fake nu / Octokit sen).
// En kurs har många labbar; varje labb delas ut till en grupp (klass som läser kursen).
public class LabService
{
    private readonly ILabRepository _labs;
    private readonly IGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly ILabProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;

    public LabService(
        ILabRepository labs,
        IGroupRepository groups,
        IUserRepository users,
        ILabProvisioner provisioner,
        IUnitOfWork unitOfWork)
    {
        _labs = labs;
        _groups = groups;
        _users = users;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
    }

    public Task<List<LabAssignment>> GetByCourseAsync(Guid courseId, CancellationToken ct) =>
        _labs.GetByCourseAsync(courseId, ct);

    /// <summary>Returnerar null om gruppen inte läser kursen (saknar GroupCourse).</summary>
    public async Task<CreatedLabResponse?> CreateAsync(Guid courseId, CreateLabDto dto, CancellationToken ct)
    {
        if (!await _groups.ReadsCourseAsync(dto.GroupId, courseId, ct))
        {
            return null;
        }

        var lab = new LabAssignment
        {
            CourseId = courseId,
            GroupId = dto.GroupId,
            Title = dto.Title,
            TemplateRepo = dto.TemplateRepo,
            UpstreamUrl = $"https://github.com/{dto.TemplateRepo}"
        };
        _labs.Add(lab);

        var students = await _users.GetStudentsInGroupAsync(dto.GroupId, ct);
        var repos = await _provisioner.ProvisionAsync(lab, students, ct);
        _labs.AddRepos(repos);

        await _unitOfWork.SaveChangesAsync(ct);
        return new CreatedLabResponse(lab.Id, UpstreamHint(lab.UpstreamUrl));
    }

    public Task<List<StudentLabRepo>> GetReposAsync(Guid labId, CancellationToken ct) =>
        _labs.GetReposAsync(labId, ct);

    public async Task RefreshChecksAsync(CancellationToken ct)
    {
        var repos = await _labs.GetAllReposAsync(ct);
        await _provisioner.RefreshCheckStatusAsync(repos, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static string UpstreamHint(string upstream) =>
        $"git remote add upstream {upstream} && git pull upstream main";
}
