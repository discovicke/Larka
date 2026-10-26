namespace Backend.Domain.Enums;

// Ett sätt att lämna in en uppgift. En uppgift kan ta emot flera (AssignmentSpec.SubmissionModes),
// och ingen alls betyder ingen inlämning, t.ex. "läs kapitel 3". En "laboration" är en uppgift med inlämning.
public enum SubmissionMode
{
    Text, // textsvar (Submission.BodyMd)
    File, // filer och foton, fungerar för alla kurser
    Repo, // GitHub-repo som skapas från en mall när uppgiften delas ut
    OnSite // läraren bedömer på plats i klassrummet
}
