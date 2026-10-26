using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Finns bara för content av typen Assignment. Deadline ligger på ContentLink.
// Med betygsskala är uppgiften en inlämning, utan (None) en övning.
public class AssignmentSpec
{
    public Guid ContentId { get; set; }
    public GradeScale GradeScale { get; set; } = GradeScale.None; // None = bara feedback
    public List<SubmissionMode> SubmissionModes { get; set; } = []; // tom = ingen inlämning
    public string? TemplateRepo { get; set; } // "org/mall", krävs när SubmissionModes har Repo
}
