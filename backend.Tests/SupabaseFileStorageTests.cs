using System.Net;
using System.Text;
using System.Text.Json;
using Backend.Application.Abstractions;
using Backend.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace backend.Tests;

// SupabaseFileStorage mot en låtsad Supabase Storage: vilka anrop som skickas och hur svaren tolkas.
public class SupabaseFileStorageTests
{
    private readonly FakeSupabase _supabase = new();

    private SupabaseFileStorage Storage => new(
        new HttpClient(_supabase) { BaseAddress = new Uri("https://abc.supabase.co/storage/v1/") },
        Options.Create(new SupabaseStorageOptions
        {
            Url = "https://abc.supabase.co",
            ServiceRoleKey = "key",
            Bucket = "files"
        }));

    [Fact]
    public async Task Upload_PostsTheBytesWithTheType_UnderANewKeyInTheFolder()
    {
        var key = await Storage.UploadAsync("content/1", new MemoryStream([1, 2, 3]), "application/pdf", default);

        Assert.StartsWith("content/1/", key);
        var request = Assert.Single(_supabase.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal($"/storage/v1/object/files/{key}", request.Path);
        Assert.Equal("application/pdf", request.ContentType);
    }

    [Fact]
    public async Task SignedUrl_IsMadeAbsoluteUnderStorageV1()
    {
        _supabase.Respond("""{"signedURL":"/object/sign/files/content/1/a?token=t"}""");

        var url = await Storage.GetSignedUrlAsync("content/1/a", TimeSpan.FromMinutes(10), default);

        Assert.Equal("https://abc.supabase.co/storage/v1/object/sign/files/content/1/a?token=t", url);
        Assert.Equal("""{"expiresIn":600}""", _supabase.Requests[0].Body);
    }

    [Fact]
    public async Task DownloadUrls_SignsEveryFileInOneRequest_WithTheRealNames()
    {
        _supabase.Respond("""
            [
              {"error":null,"path":"s/1/a","signedURL":"/object/sign/files/s/1/a?token=a"},
              {"error":null,"path":"s/1/b","signedURL":"/object/sign/files/s/1/b?token=b"}
            ]
            """);

        var urls = await Storage.GetDownloadUrlsAsync(
            [new StoredFile("s/1/a", "rapport.pdf"), new StoredFile("s/1/b", "bild av mig.png")],
            TimeSpan.FromMinutes(10),
            default);

        var request = Assert.Single(_supabase.Requests);
        Assert.Equal("/storage/v1/object/sign/files", request.Path);
        Assert.Equal("""{"expiresIn":600,"paths":["s/1/a","s/1/b"]}""", request.Body);
        Assert.Equal(
            "https://abc.supabase.co/storage/v1/object/sign/files/s/1/a?token=a&download=rapport.pdf", urls["s/1/a"]);
        Assert.EndsWith("&download=bild%20av%20mig.png", urls["s/1/b"]);
    }

    [Fact]
    public async Task DownloadUrls_ForNoFiles_SendsNothing()
    {
        var urls = await Storage.GetDownloadUrlsAsync([], TimeSpan.FromMinutes(10), default);

        Assert.Empty(urls);
        Assert.Empty(_supabase.Requests);
    }

    [Fact]
    public async Task DownloadUrls_WhenAFileIsMissing_Throws()
    {
        _supabase.Respond("""
            [{"error":"Either the object does not exist","path":"s/1/a","signedURL":null}]
            """);

        await Assert.ThrowsAsync<FileNotFoundException>(() => Storage.GetDownloadUrlsAsync(
            [new StoredFile("s/1/a", "a.pdf")], TimeSpan.FromMinutes(10), default));
    }

    [Fact]
    public async Task Copy_PostsSourceAndANewKeyInTheFolder()
    {
        var key = await Storage.CopyAsync("content/1/a", "content/2", default);

        Assert.StartsWith("content/2/", key);
        var request = Assert.Single(_supabase.Requests);
        Assert.Equal("/storage/v1/object/copy", request.Path);
        Assert.Equal(
            $$"""{"bucketId":"files","sourceKey":"content/1/a","destinationKey":"{{key}}"}""", request.Body);
    }

    [Fact]
    public async Task Delete_UsesTheBulkEndpoint_ThatAcceptsMissingFiles()
    {
        _supabase.Respond("[]");

        await Storage.DeleteAsync("content/1/a", default);

        var request = Assert.Single(_supabase.Requests);
        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/storage/v1/object/files", request.Path);
        Assert.Equal("""{"prefixes":["content/1/a"]}""", request.Body);
    }

    [Fact]
    public async Task AnErrorFromSupabase_Throws()
    {
        _supabase.Respond("""{"statusCode":"403","error":"Unauthorized"}""", HttpStatusCode.Forbidden);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Storage.UploadAsync("content/1", new MemoryStream([1]), "text/plain", default));
    }

    private sealed record SentRequest(string Method, string Path, string? ContentType, string Body);

    private sealed class FakeSupabase : HttpMessageHandler
    {
        private string _body = "{}";
        private HttpStatusCode _status = HttpStatusCode.OK;

        public List<SentRequest> Requests { get; } = [];

        public void Respond(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Content?.Headers.ContentType?.MediaType == "application/json")
            {
                // Samma form oavsett hur serialiseraren formaterar, så att testerna jämför innehållet.
                body = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(body));
            }

            Requests.Add(new SentRequest(
                request.Method.Method,
                request.RequestUri!.AbsolutePath,
                request.Content?.Headers.ContentType?.MediaType,
                body));
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
