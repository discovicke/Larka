namespace Backend.Domain.Entities;

// Kopplar grupp <-> kurs (M-N): en klass läser flera kurser,
// en kurs kan läsas av flera klasser. Committee-grupper har inga rader här.
public class GroupCourse
{
    public Guid GroupId { get; set; }
    public Guid CourseId { get; set; }
}
