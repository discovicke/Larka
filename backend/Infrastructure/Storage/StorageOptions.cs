namespace Backend.Infrastructure.Storage;

// Storage:Supabase i konfigurationen. ServiceRoleKey är en hemlighet och ligger i Infisical, aldrig i appsettings.
public class SupabaseStorageOptions
{
    public const string SectionName = "Storage:Supabase";

    public string Url { get; set; } = "";
    public string Bucket { get; set; } = "files";
    public string ServiceRoleKey { get; set; } = "";

    public bool IsConfigured => Url != "" && ServiceRoleKey != "";
}

// Storage:Disk i konfigurationen. Path är mappen där filerna sparas, till exempel en volym i Docker. Används när
// Supabase inte är inställt.
public class DiskStorageOptions
{
    public const string SectionName = "Storage:Disk";

    public string Path { get; set; } = "";

    public bool IsConfigured => Path != "";
}
