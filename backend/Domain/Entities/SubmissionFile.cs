namespace Backend.Domain.Entities;

public class SubmissionFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public long SizeBytes { get; set; }
}
