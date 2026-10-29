namespace Backend.Domain.Entities;

/// <summary>
/// Skolans GitHub App-installation. En rad per skola.
/// Connected=false betyder att admin lokalt kopplat från; appen är fortfarande installerad på GitHub.
/// </summary>
public class SchoolGitHubInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SchoolId { get; set; }
    public long InstallationId { get; set; }
    public string Org { get; set; } = "";
    public bool Connected { get; set; }

    /// <summary>Gör att admin kan pausa GitHub utan att koppla från installationen.</summary>
    public bool Enabled { get; set; } = true;
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset? DisconnectedAt { get; set; }

    /// <summary>Senaste gången installationen verifierades mot GitHub.</summary>
    public DateTimeOffset? LastVerifiedAt { get; set; }
}
