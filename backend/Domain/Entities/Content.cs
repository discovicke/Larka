using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Nyheter, teori och uppgifter. Lever fristående och publiceras till grupper och kurser via ContentLink,
// så samma material kan återanvändas för flera klasser.
public class Content
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public ContentType Type { get; set; }
    public string Title { get; set; } = "";
    public string BodyMd { get; set; } = "";
    public ContentStatus Status { get; set; } = ContentStatus.Published;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public AssignmentConfig? Assignment { get; set; }
}
