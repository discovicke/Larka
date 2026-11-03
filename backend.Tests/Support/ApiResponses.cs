namespace backend.Tests;

// Svar som många tester läser. Saknas ett fält i svaret blir det default, så en test läser bara de fält den behöver.

public record IdResponse(Guid Id);

public record ModuleResponse(
    Guid Id, Guid CourseId, string Title, string Description, int Position, DateTimeOffset? PublishAt);

public record ContentResponse(
    Guid Id, string Type, string Title, Guid AuthorId, string BodyMd, string Status);

public record LinkResponse(Guid Id, Guid ContentId, Guid? CourseId, Guid? ModuleId);
