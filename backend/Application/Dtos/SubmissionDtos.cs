namespace Backend.Application.Dtos;

public record SubmitDto(string BodyMd, string? RepoUrl);

public record GradeDto(int Score, string Feedback);
