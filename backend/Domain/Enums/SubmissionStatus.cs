namespace Backend.Domain.Enums;

public enum SubmissionStatus
{
    NotStarted, // skapas när uppgiften delas ut
    Submitted,
    NeedsWork, // läraren har begärt komplettering
    Graded
}
