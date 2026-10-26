using Backend.Application.Dtos;
using Backend.Application.Files;

namespace backend.Tests;

public class FileUploadPolicyTests
{
    private static FileUpload File(string name, long sizeBytes = 3) => new(name, sizeBytes, Stream.Null);

    [Theory]
    [InlineData("slides.pdf", "application/pdf")]
    [InlineData("SCREENSHOT.PNG", "image/png")]
    [InlineData("foto.jpeg", "image/jpeg")]
    [InlineData("rapport.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("projekt.zip", "application/zip")]
    public void ContentTypeFor_AllowedExtension_ReturnsItsType(string fileName, string expected) =>
        Assert.Equal(expected, FileUploadPolicy.ContentTypeFor(fileName));

    [Theory]
    [InlineData("index.html")]
    [InlineData("vector.svg")]
    [InlineData("app.js")]
    [InlineData("noextension")]
    public void ContentTypeFor_DisallowedExtension_ReturnsNull(string fileName) =>
        Assert.Null(FileUploadPolicy.ContentTypeFor(fileName));

    [Fact]
    public void CleanFileName_KeepsOnlyTheNameNotThePath() =>
        Assert.Equal("x.pdf", FileUploadPolicy.CleanFileName("../../etc/x.pdf"));

    [Fact]
    public void Validate_AllowedFiles_ReturnsNull() =>
        Assert.Null(FileUploadPolicy.Validate([File("a.pdf"), File("b.png")]));

    [Fact]
    public void Validate_FileAtTheSizeLimit_IsAllowed() =>
        Assert.Null(FileUploadPolicy.Validate([File("a.pdf", FileUploadPolicy.MaxFileBytes)]));

    [Fact]
    public void Validate_FileOverTheSizeLimit_ReturnsError() =>
        Assert.NotNull(FileUploadPolicy.Validate([File("a.pdf", FileUploadPolicy.MaxFileBytes + 1)]));

    [Fact]
    public void Validate_EmptyFile_ReturnsError() =>
        Assert.NotNull(FileUploadPolicy.Validate([File("a.pdf", 0)]));

    [Fact]
    public void Validate_DisallowedType_ReturnsError() =>
        Assert.NotNull(FileUploadPolicy.Validate([File("a.pdf"), File("a.exe")]));

    [Fact]
    public void Validate_TooManyFiles_ReturnsError()
    {
        var files = Enumerable.Range(0, FileUploadPolicy.MaxFilesPerRequest + 1)
            .Select(number => File($"{number}.pdf"))
            .ToList();

        Assert.NotNull(FileUploadPolicy.Validate(files));
    }

    [Fact]
    public void Validate_FilesTogetherOverTheRequestLimit_ReturnsError()
    {
        var files = Enumerable.Range(0, 3)
            .Select(number => File($"{number}.pdf", FileUploadPolicy.MaxFileBytes))
            .ToList();

        Assert.NotNull(FileUploadPolicy.Validate(files));
    }
}
