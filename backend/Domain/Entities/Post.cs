using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid AuthorId { get; set; }
    public PostType Type { get; set; }
    public string Title { get; set; } = "";
    public string BodyMd { get; set; } = "";
    public DateTimeOffset PublishAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsPinned { get; set; }
    public AssignmentConfig? Assignment { get; set; }
}
