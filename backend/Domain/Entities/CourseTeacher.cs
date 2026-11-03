namespace Backend.Domain.Entities;

// Läraren i en kurs. Det är den här kopplingen, inte klassen, som avgör vem som får redigera och bedöma kursen:
// klassen har elever, kursen har sina lärare. Läraren behöver inte vara medlem i klassen.
public class CourseTeacher
{
    public Guid CourseId { get; set; }
    public Guid UserId { get; set; }
}
