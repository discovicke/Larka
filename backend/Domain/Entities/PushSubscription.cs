namespace Backend.Domain.Entities;

// En enhet (mobil, dator) där användaren har tillåtit notiser. Endpoint och nycklarna kommer från
// webbläsarens Push API; backend skickar web push dit. En användare kan ha flera.
public class PushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
