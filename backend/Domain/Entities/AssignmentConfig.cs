namespace Backend.Domain.Entities;

// Finns bara för content av typen Assignment. Deadline ligger på ContentLink.
public class AssignmentConfig
{
    public Guid ContentId { get; set; }
    public int MaxScore { get; set; } = 100;
}
