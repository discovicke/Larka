namespace Backend.Application.Dtos;

public record CreateLabDto(string Title, string TemplateRepo, Guid GroupId);

public record CreatedLabResponse(Guid Id, string UpstreamHint);
