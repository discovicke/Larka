using System.Net;
using Backend.Application.Abstractions;
using Backend.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

/// <summary>Appen med filerna på disk (Storage:Disk:Path) i en tillfällig mapp, som när en skola kör Lärka själv.</summary>
public sealed class DiskStorageWebApplicationFactory : TestWebApplicationFactory
{
    public string FilesPath { get; } = Path.Combine(Path.GetTempPath(), $"larka-files-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Storage:Disk:Path", FilesPath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(FilesPath))
        {
            Directory.Delete(FilesPath, recursive: true);
        }
    }
}

public class DiskFileStorageTests(DiskStorageWebApplicationFactory factory)
    : IClassFixture<DiskStorageWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private IFileStorage Storage => factory.Services.GetRequiredService<IFileStorage>();

    [Fact]
    public void DiskPath_SelectsDiskStorage()
    {
        Assert.IsType<DiskFileStorage>(Storage);
    }

    [Fact]
    public async Task Upload_ThenSignedLink_ServesTheFileWithItsType_WithoutAToken()
    {
        var key = await Storage.UploadAsync("content/abc", new MemoryStream("hej"u8.ToArray()), "text/plain", default);

        var url = await Storage.GetSignedUrlAsync(key, IFileStorage.SignedUrlLifetime, default);
        var response = await _client.GetAsync(url);

        Assert.StartsWith($"/files/{key}?", url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hej", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("expired")]
    [InlineData("other-key")]
    [InlineData("traversal")]
    [InlineData("missing-signature")]
    public async Task InvalidLinks_Return404(string problem)
    {
        var key = await Storage.UploadAsync("content/abc", new MemoryStream([1, 2, 3]), "image/png", default);
        var other = await Storage.UploadAsync("content/abc", new MemoryStream([4]), "image/png", default);
        var url = await Storage.GetSignedUrlAsync(key, IFileStorage.SignedUrlLifetime, default);
        var expired = await Storage.GetSignedUrlAsync(key, TimeSpan.FromMinutes(-1), default);

        var path = problem switch
        {
            "tampered" => url[..^2] + "xx",
            "expired" => expired,
            "other-key" => url.Replace(key, other),
            "traversal" => $"/files/content/../../etc/passwd{url[url.IndexOf('?')..]}",
            _ => $"/files/{key}"
        };
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Copy_KeepsTheContentAndType_AndDelete_RemovesOnlyTheOriginal()
    {
        var key = await Storage.UploadAsync("content/abc", new MemoryStream("kopia"u8.ToArray()), "text/csv", default);

        var copy = await Storage.CopyAsync(key, "content/def", default);
        await Storage.DeleteAsync(key, default);
        var copyUrl = await Storage.GetSignedUrlAsync(copy, TimeSpan.FromMinutes(1), default);
        var copyResponse = await _client.GetAsync(copyUrl);

        Assert.StartsWith("content/def/", copy);
        Assert.Equal("kopia", await copyResponse.Content.ReadAsStringAsync());
        Assert.Equal("text/csv", copyResponse.Content.Headers.ContentType?.MediaType);
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => Storage.GetSignedUrlAsync(key, TimeSpan.FromMinutes(1), default));
    }
}
