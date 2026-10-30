namespace Backend.Application.Dtos;

public record SetStudentHelpDto(string? Message = null);

public record StudentHelpStatusDto(
    bool Requested,
    string? Message,
    DateTimeOffset? CreatedAt,
    bool Resolved);
