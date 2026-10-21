namespace Backend.Application.Dtos;

public record CreatePostDto(
    string Type,
    string Title,
    string BodyMd,
    bool IsPinned = false,
    DateTimeOffset? DueAt = null,
    int? MaxScore = null);

public record CommentDto(string BodyMd);

public record AttachmentDto(string FileName, string Url, long SizeBytes);
