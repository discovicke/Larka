namespace Backend.Domain.Entities;

public class School
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    // Skolans tenant i Microsoft Entra ID. Konton därifrån kan logga in med Microsoft. Null tills admin har
    // kopplat skolan.
    public string? EntraTenantId { get; set; }
}
