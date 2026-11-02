using Backend.Application.Dtos;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

public static class AssignmentFieldsValidator
{
    public static string? Check(
        ContentType type,
        GradeScale? gradeScale,
        List<SubmissionMode>? modes,
        string? templateRepo,
        string? repoNamePattern,
        RepoWorkMode workMode,
        bool feedbackPullRequest,
        bool allowLate,
        List<AssignmentRequirementInput>? requirements)
    {
        var hasAssignmentFields = gradeScale is not null
            || modes is not null
            || templateRepo is not null
            || !string.IsNullOrWhiteSpace(repoNamePattern)
            || feedbackPullRequest
            || allowLate
            || workMode != RepoWorkMode.Individual
            || requirements is not null && requirements.Count > 0;
        if (type != ContentType.Assignment && hasAssignmentFields)
        {
            return "gradeScale, submissionModes and GitHub settings are only allowed on assignments";
        }

        var submissionModes = modes ?? [];
        var usesRepo = StudentWorkPolicy.UsesRepo(submissionModes);
        if (usesRepo != (templateRepo is not null))
        {
            return "templateRepo is required for repo, and only allowed there";
        }

        if (!usesRepo)
        {
            return !string.IsNullOrWhiteSpace(repoNamePattern)
                || feedbackPullRequest
                || allowLate
                || workMode != RepoWorkMode.Individual
                ? "repoNamePattern and repo work settings are only allowed with repo submission mode"
                : null;
        }

        if (feedbackPullRequest)
        {
            return "feedback pull requests are not supported yet";
        }

        if (allowLate)
        {
            return "late submissions are not supported yet";
        }

        if (!LooksLikeOrgAndName(templateRepo!))
        {
            return "templateRepo must look like org/name";
        }

        if (requirements is not null && requirements.Count > 0)
        {
            foreach (var requirement in requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement.Title))
                {
                    return "every requirement needs a title";
                }

                if (!requirement.IsManual && string.IsNullOrWhiteSpace(requirement.TestPattern))
                {
                    return "requirements that are not manual need a test pattern";
                }
            }
        }

        return GitHubRepoNaming.Validate(repoNamePattern, workMode);
    }

    // "org/name": exakt ett snedstreck, med text på båda sidor.
    private static bool LooksLikeOrgAndName(string repo)
    {
        var parts = repo.Split('/');
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0;
    }
}
