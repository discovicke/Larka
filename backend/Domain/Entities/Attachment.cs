namespace Backend.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public string FileName { get; set; } = "";
    public string Url { get; set; } = "";
    public long SizeBytes { get; set; }
}
