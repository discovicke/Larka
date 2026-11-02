namespace Backend.Application.Dtos;

/// <summary>Användarnamn eller e-post, och lösenord. Jämförs med e-posten utan hänsyn till versaler.</summary>
public record LoginDto(string Username, string Password);

public record LoginResultDto(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>ID-tokenet som klienten fick av Microsoft (MSAL).</summary>
public record MicrosoftLoginDto(string IdToken);

/// <summary>Lärkas token, som vid inloggning med lösenord. FirstSignIn är sant när användaren loggar in första gången.</summary>
public record MicrosoftLoginResultDto(string AccessToken, DateTimeOffset ExpiresAt, bool FirstSignIn);

/// <summary>Microsoft är null när Microsoft-inloggning inte är inställd, och då visar klienten bara lösenordet.</summary>
public record AuthProvidersDto(MicrosoftProviderDto? Microsoft);

public record MicrosoftProviderDto(string ClientId, string TenantId);
