namespace Backend.Api;

// Inlämning med filer, som multipart/form-data: textfältet bodyMd och en eller flera filer under files.
public class SubmitForm
{
    public string? BodyMd { get; set; }

    public IFormFileCollection? Files { get; set; }
}
