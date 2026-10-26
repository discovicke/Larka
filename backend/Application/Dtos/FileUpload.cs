namespace Backend.Application.Dtos;

/// <summary>
/// En fil som ska sparas. Skapas av uppladdnings-endpointsen. Typen som lagras med sätts från filnamnet
/// (se FileUploadPolicy), inte från något klienten skickar.
/// </summary>
public record FileUpload(string FileName, long SizeBytes, Stream Content);
