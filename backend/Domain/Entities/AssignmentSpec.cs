using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Finns bara för content av typen Assignment. Deadline ligger på ContentLink.
// Med betygsskala är uppgiften en inlämning, utan (None) en övning.
public class AssignmentSpec
{
    public Guid ContentId { get; set; }
    public GradeScale GradeScale { get; set; } = GradeScale.None; // None = bara feedback
    public List<SubmissionMode> SubmissionModes { get; set; } = []; // tom = ingen inlämning
    public CompletionRule CompletionRule { get; set; } = CompletionRule.Submitted; // när nästa del låses upp
    public string? TemplateRepo { get; set; } // "org/mall", krävs när SubmissionModes har Repo

    // GitHub-inställningar. Null/standardvärden används när uppgiften inte har Repo som inlämningssätt.
    public string? RepoNamePattern { get; set; } // t.ex. "lab2-{githubLogin}"; null = GitHubRepoNaming.DefaultPattern
    public RepoWorkMode WorkMode { get; set; } = RepoWorkMode.Individual;
    public bool LockAtDeadline { get; set; } = true;
    public bool FeedbackPullRequest { get; set; } // fas 2
    public bool AllowLate { get; set; } // fas 2
}
