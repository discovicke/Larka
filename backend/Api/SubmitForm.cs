namespace Backend.Api;

// Inlämning med filer, som multipart/form-data: textfältet bodyMd och en eller flera filer under files.
public class SubmitForm
{
    public string? BodyMd { get; set; }

    public IFormFileCollection? Files { get; set; }

    // Redan inlämnade filer som ska finnas kvar. Utan fältet ersätts alla filer av de nya.
    public List<Guid>? KeepFileIds { get; set; }
}
