using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Finns bara för content av typen Assignment. Deadline ligger på ContentLink.
public class AssignmentSpec
{
    public Guid ContentId { get; set; }
    public int? MaxScore { get; set; } // null = bara feedback, inga poäng
    public SubmissionMode SubmissionMode { get; set; } = SubmissionMode.None;
    public string? TemplateRepo { get; set; } // "org/mall", krävs när SubmissionMode använder repo
}
