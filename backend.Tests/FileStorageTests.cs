using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Application.Services;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Servicerna mot lagringen i minnet, så ingen Supabase behövs.
public class FileStorageTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private InMemoryFileStorage Storage => (InMemoryFileStorage)factory.Services.GetRequiredService<IFileStorage>();

    private record IdResponse(Guid Id);

    private record AttachmentResponse(Guid Id, string FileName, string Url, long SizeBytes);

    private record FileResponse(string FileName, string Url);

    private record SubmissionResponse(List<FileResponse> Files);

    private static FileUpload Upload(string name) => new(name, 3, new MemoryStream([1, 2, 3]));

    [Fact]
    public async Task AddAttachment_StoresFileUnderContentKey_AndReturnsSignedUrl()
    {
        var contentId = await CreateContentAsync("theory");

        var result = await WithServiceAsync<ContentService, ServiceResult<AttachmentDto>>(
            service => service.AddAttachmentAsync(contentId, Upload("slides.pdf"), CancellationToken.None));

        Assert.Equal(ResultStatus.Ok, result.Status);
        var attachment = result.Value!;
        var key = Assert.Single(Storage.Keys, key => key.StartsWith($"content/{contentId}/"));
        Assert.Contains(key, attachment.Url);
        Assert.Equal("slides.pdf", attachment.FileName);
    }

    [Fact]
    public async Task AddAttachment_ToMissingContent_StoresNothing()
    {
        var before = Storage.Keys.Count;

        var result = await WithServiceAsync<ContentService, ServiceResult<AttachmentDto>>(
            service => service.AddAttachmentAsync(Guid.NewGuid(), Upload("x.pdf"), CancellationToken.None));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(before, Storage.Keys.Count);
    }

    [Fact]
    public async Task GetAttachments_ReturnsFreshSignedUrls_AndNeverTheStorageKeyAsUrl()
    {
        var contentId = await CreateContentAsync("theory");
        await WithServiceAsync<ContentService, ServiceResult<AttachmentDto>>(
            service => service.AddAttachmentAsync(contentId, Upload("a.pdf"), CancellationToken.None));

        var attachments = await _client.GetFromJsonAsync<List<AttachmentResponse>>(
            $"/api/content/{contentId}/attachments");

        var attachment = Assert.Single(attachments!);
        Assert.StartsWith("https://", attachment.Url);
        Assert.Contains("expires=", attachment.Url);
    }

    [Fact]
    public async Task GetAttachments_ForMissingContent_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/content/{Guid.NewGuid()}/attachments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAttachment_RemovesRowAndFile()
    {
        var contentId = await CreateContentAsync("theory");
        var result = await WithServiceAsync<ContentService, ServiceResult<AttachmentDto>>(
            service => service.AddAttachmentAsync(contentId, Upload("a.pdf"), CancellationToken.None));
        Assert.Single(Storage.Keys, key => key.StartsWith($"content/{contentId}/"));

        var response = await _client.DeleteAsync($"/api/content/{contentId}/attachments/{result.Value!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(Storage.Keys, key => key.StartsWith($"content/{contentId}/"));
        var left = await _client.GetFromJsonAsync<List<AttachmentResponse>>($"/api/content/{contentId}/attachments");
        Assert.Empty(left!);
    }

    [Fact]
    public async Task DeleteAttachment_ThatDoesNotExist_ReturnsNotFound()
    {
        var contentId = await CreateContentAsync("theory");

        var response = await _client.DeleteAsync($"/api/content/{contentId}/attachments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Submit_StoresFilesUnderSubmissionKey_AndResubmitReplacesThem()
    {
        var linkId = await CreateFileAssignmentLinkAsync();

        var first = await SubmitAsync(linkId, "v1.pdf");
        var firstKey = Assert.Single(Storage.Keys, key => key.StartsWith($"submissions/{first.Id}/"));
        Assert.Contains(firstKey, Assert.Single(first.Files).Url);

        var second = await SubmitAsync(linkId, "v2.pdf");

        Assert.Equal(first.Id, second.Id);
        var secondKey = Assert.Single(Storage.Keys, key => key.StartsWith($"submissions/{second.Id}/"));
        Assert.NotEqual(firstKey, secondKey);
        Assert.Equal("v2.pdf", Assert.Single(second.Files).FileName);
    }

    [Fact]
    public async Task Submit_Rejected_StoresNothing()
    {
        var linkId = await CreateFileAssignmentLinkAsync(modes: ["text"]);
        var before = Storage.Keys.Count;

        var result = await WithServiceAsync<SubmissionService, ServiceResult<SubmissionDto>>(service =>
            service.SubmitAsync(linkId, new SubmitDto("svar"), [Upload("x.pdf")], CancellationToken.None));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(before, Storage.Keys.Count);
    }

    [Fact]
    public async Task ReadingSubmission_ReturnsSignedUrlForItsFile()
    {
        var linkId = await CreateFileAssignmentLinkAsync();
        var submitted = await SubmitAsync(linkId, "rapport.pdf");

        var read = await _client.GetFromJsonAsync<SubmissionResponse>($"/api/submissions/{submitted.Id}");

        var file = Assert.Single(read!.Files);
        Assert.Contains($"submissions/{submitted.Id}/", file.Url);
        Assert.Contains("expires=", file.Url);
    }

    private async Task<SubmissionDto> SubmitAsync(Guid linkId, string fileName)
    {
        var result = await WithServiceAsync<SubmissionService, ServiceResult<SubmissionDto>>(service =>
            service.SubmitAsync(linkId, new SubmitDto(), [Upload(fileName)], CancellationToken.None));
        Assert.Equal(ResultStatus.Ok, result.Status);
        return result.Value!;
    }

    private async Task<TResult> WithServiceAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private async Task<Guid> CreateContentAsync(string type)
    {
        var response = await _client.PostAsJsonAsync("/api/content", new { type, title = "Material", bodyMd = "" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> CreateFileAssignmentLinkAsync(string[]? modes = null)
    {
        Guid groupId;
        using (var scope = factory.Services.CreateScope())
        {
            groupId = (await TestData.AddGroupAsync(
                scope.ServiceProvider.GetRequiredService<AppDb>(), placeholderTeaches: true)).Id;
        }

        var content = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title = "Labb",
            bodyMd = "",
            submissionModes = modes ?? ["file"]
        });
        content.EnsureSuccessStatusCode();
        var contentId = (await content.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var link = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", new { groupId });
        link.EnsureSuccessStatusCode();
        return (await link.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }
}
