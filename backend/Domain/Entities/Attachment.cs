namespace Backend.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public long SizeBytes { get; set; }
}
