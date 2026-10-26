using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Finns bara för content av typen Assignment. Deadline ligger på ContentLink.
public class AssignmentSpec
{
    public Guid ContentId { get; set; }
    public AssignmentKind Kind { get; set; }
    public GradeScale GradeScale { get; set; } = GradeScale.None; // None = bara feedback, alltid för Exercise
    public List<SubmissionMode> SubmissionModes { get; set; } = []; // tom = ingen inlämning
    public string? TemplateRepo { get; set; } // "org/mall", krävs när SubmissionModes har Repo
}
