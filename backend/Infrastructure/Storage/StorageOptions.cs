namespace Backend.Infrastructure.Storage;

// Storage:Supabase i konfigurationen. ServiceRoleKey är en hemlighet: user-secrets lokalt och miljövariabeln
// Storage__Supabase__ServiceRoleKey på Render, aldrig i appsettings.
public class SupabaseStorageOptions
{
    public const string SectionName = "Storage:Supabase";

    public string Url { get; set; } = "";
    public string Bucket { get; set; } = "files";
    public string ServiceRoleKey { get; set; } = "";

    public bool IsConfigured => Url != "" && ServiceRoleKey != "";
}
