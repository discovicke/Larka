namespace Backend.Domain.Enums;

// Hur man lämnar in en uppgift. En "laboration" är en uppgift med inlämning.
public enum SubmissionMode
{
    None, // ingen inlämning, t.ex. "läs kapitel 3"
    File, // filinlämning, fungerar för alla kurser
    Repo, // GitHub-repo som skapas från en mall när uppgiften delas ut
    FileOrRepo
}
