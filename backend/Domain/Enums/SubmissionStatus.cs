namespace Backend.Domain.Enums;

public enum SubmissionStatus
{
    NotStarted, // skapas när uppgiften delas ut
    Submitted,
    NeedsWork, // läraren har begärt komplettering
    Graded,
    NotSubmitted // läraren har markerat att eleven inte lämnat in
}
