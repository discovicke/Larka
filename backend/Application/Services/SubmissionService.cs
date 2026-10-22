using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

public class SubmissionService
{
    private readonly ISubmissionRepository _submissions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public SubmissionService(ISubmissionRepository submissions, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _submissions = submissions;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Submission> SubmitAsync(Guid contentId, SubmitDto dto, CancellationToken ct)
    {
        var submission = new Submission
        {
            ContentId = contentId,
            StudentId = _currentUser.UserId,
            BodyMd = dto.BodyMd,
            RepoUrl = dto.RepoUrl
        };
        _submissions.Add(submission);
        await _unitOfWork.SaveChangesAsync(ct);
        return submission;
    }

    public async Task GradeAsync(Guid submissionId, GradeDto dto, CancellationToken ct)
    {
        _submissions.AddGrade(new Grade
        {
            SubmissionId = submissionId,
            ByTeacherId = _currentUser.UserId,
            Score = dto.Score,
            Feedback = dto.Feedback
        });
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
