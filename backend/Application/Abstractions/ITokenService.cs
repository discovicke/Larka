using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

/// <summary>
/// Skapar tokenet som klienten skickar med i Authorization-headern. Egen implementation nu, och
/// Microsoft Entra ID senare, utan att inloggningen behöver känna till skillnaden.
/// </summary>
public interface ITokenService
{
    /// <summary>När tokenet som skapades senast slutar gälla.</summary>
    DateTimeOffset ExpiresAt { get; }

    string CreateToken(User user);
}
