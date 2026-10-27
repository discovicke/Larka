namespace Backend.Application.Dtos;

/// <summary>Användarnamn eller e-post, och lösenord. Jämförs med e-posten utan hänsyn till versaler.</summary>
public record LoginDto(string Username, string Password);

public record LoginResultDto(string AccessToken, DateTimeOffset ExpiresAt);
