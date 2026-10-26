namespace Backend.Application.Dtos;

/// <summary>En fil som ska sparas. Uppladdnings-endpointsen som skapar den kommer i #47.</summary>
public record FileUpload(string FileName, string ContentType, long SizeBytes, Stream Content);
