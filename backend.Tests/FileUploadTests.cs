using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Uppladdning via HTTP (multipart/form-data) och vem som får läsa en inlämnings filer.
public class FileUploadTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private InMemoryFileStorage Storage => (InMemoryFileStorage)factory.Services.GetRequiredService<IFileStorage>();

    private record IdResponse(Guid Id);

    private record AttachmentResponse(Guid Id, string FileName, string Url, long SizeBytes);

    private record FileResponse(Guid Id, string FileName, string Url);

    private record SubmissionResponse(Guid Id, Guid StudentId, string Status, List<FileResponse> Files);

    private record FileAssignment(Guid LinkId, List<Guid> Students, List<Guid> Teachers);

    [Fact]
    public async Task UploadAttachment_StoresFileUnderContentKey_AndReturnsIt()
    {
        var contentId = await CreateContentAsync();

        var response = await _client.PostAsync(
            $"/api/content/{contentId}/attachments", FileForm("slides.pdf", field: "file"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attachment = (await response.Content.ReadFromJsonAsync<AttachmentResponse>())!;
        Assert.Equal("slides.pdf", attachment.FileName);
        Assert.Equal(3, attachment.SizeBytes);
        Assert.Single(Storage.Keys, key => key.StartsWith($"content/{contentId}/"));
    }

    [Theory]
    [InlineData("payload.html")]
    [InlineData("script.js")]
    [InlineData("noextension")]
    public async Task UploadAttachment_WithDisallowedType_ReturnsBadRequest_AndStoresNothing(string fileName)
    {
        var contentId = await CreateContentAsync();
        var before = Storage.Keys.Count;

        var response = await _client.PostAsync(
            $"/api/content/{contentId}/attachments", FileForm(fileName, field: "file"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, Storage.Keys.Count);
    }

    [Fact]
    public async Task UploadAttachment_ToMissingContent_ReturnsNotFound()
    {
        var response = await _client.PostAsync(
            $"/api/content/{Guid.NewGuid()}/attachments", FileForm("a.pdf", field: "file"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Submit_WithTextAndFiles_StoresFilesAndReturnsThem()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 1);
        var student = assignment.Students[0];

        var response = await SendAsync(HttpMethod.Post, $"/api/links/{assignment.LinkId}/submissions", student,
            FileForm("rapport.pdf", bodyMd: "Se bilagan"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var submission = (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!;
        Assert.Equal("Submitted", submission.Status);
        Assert.Equal("rapport.pdf", Assert.Single(submission.Files).FileName);
        Assert.Single(Storage.Keys, key => key.StartsWith($"submissions/{submission.Id}/"));
    }

    [Fact]
    public async Task Resubmit_WithKeepFileIdsInTheForm_KeepsThatFile()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 1);
        var student = assignment.Students[0];
        var first = await SubmitFileAsync(assignment.LinkId, student);
        var form = FileForm("ny.pdf");
        form.Add(new StringContent(first.Files[0].Id.ToString()), "keepFileIds");

        var response = await SendAsync(HttpMethod.Post, $"/api/links/{assignment.LinkId}/submissions", student, form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Files;
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public async Task Submit_WithDisallowedFile_ReturnsBadRequest_AndStoresNothing()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 1);
        var before = Storage.Keys.Count;

        var response = await SendAsync(HttpMethod.Post, $"/api/links/{assignment.LinkId}/submissions",
            assignment.Students[0], FileForm("exe.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, Storage.Keys.Count);
    }

    [Fact]
    public async Task ReadingSubmission_ByItsStudent_ReturnsFiles()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 1);
        var submitted = await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);

        var response = await SendAsync(HttpMethod.Get, $"/api/submissions/{submitted.Id}", assignment.Students[0]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Files);
    }

    [Fact]
    public async Task ReadingSubmission_ByAnotherStudent_ReturnsNotFound()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 2);
        var submitted = await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);

        var response = await SendAsync(HttpMethod.Get, $"/api/submissions/{submitted.Id}", assignment.Students[1]);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReadingSubmission_ByTeacherOfTheGroup_ReturnsFiles()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 1, teacherCount: 1);
        var submitted = await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);

        var response = await SendAsync(HttpMethod.Get, $"/api/submissions/{submitted.Id}", assignment.Teachers[0]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Files);
    }

    [Fact]
    public async Task ReadingSubmission_ByTeacherOfAnotherGroup_ReturnsNotFound()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 1);
        var submitted = await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);
        var outsider = await AddTeacherOfNewGroupAsync();

        var response = await SendAsync(HttpMethod.Get, $"/api/submissions/{submitted.Id}", outsider);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Grading_ByAStudent_ReturnsForbidden_AndGradesNothing()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 2);
        var submitted = await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);

        var response = await SendAsync(HttpMethod.Post, $"/api/submissions/{submitted.Id}/grade",
            assignment.Students[1], JsonContent.Create(new { feedback = "Bra" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var own = await SendAsync(HttpMethod.Get, $"/api/submissions/{submitted.Id}", assignment.Students[0]);
        Assert.Equal("Submitted", (await own.Content.ReadFromJsonAsync<SubmissionResponse>())!.Status);
    }

    [Fact]
    public async Task Grading_ByTeacherOfTheGroup_Works()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 1, teacherCount: 1);
        var submitted = await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);

        var response = await SendAsync(HttpMethod.Post, $"/api/submissions/{submitted.Id}/grade",
            assignment.Teachers[0], JsonContent.Create(new { feedback = "Bra" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Graded", (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Status);
    }

    [Fact]
    public async Task ListingSubmissions_ByStudent_ShowsOnlyTheirOwn()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 2);
        await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);
        await SubmitFileAsync(assignment.LinkId, assignment.Students[1]);

        var response = await SendAsync(
            HttpMethod.Get, $"/api/links/{assignment.LinkId}/submissions", assignment.Students[0]);

        var listed = (await response.Content.ReadFromJsonAsync<List<SubmissionResponse>>())!;
        Assert.Equal(assignment.Students[0], Assert.Single(listed).StudentId);
    }

    [Fact]
    public async Task ListingSubmissions_ByTeacherOfTheGroup_ShowsEveryone()
    {
        var assignment = await CreateFileAssignmentAsync(studentCount: 2, teacherCount: 1);
        await SubmitFileAsync(assignment.LinkId, assignment.Students[0]);
        await SubmitFileAsync(assignment.LinkId, assignment.Students[1]);

        var response = await SendAsync(
            HttpMethod.Get, $"/api/links/{assignment.LinkId}/submissions", assignment.Teachers[0]);

        var listed = (await response.Content.ReadFromJsonAsync<List<SubmissionResponse>>())!;
        Assert.Equal(2, listed.Count);
    }

    // Skapar en uppgift med filinlämning, delad till en ny grupp med elever och lärare.
    private async Task<FileAssignment> CreateFileAssignmentAsync(int studentCount, int teacherCount = 0)
    {
        var students = new List<Guid>();
        var teachers = new List<Guid>();
        Guid groupId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var group = await TestData.AddGroupAsync(db, placeholderTeaches: true);
            groupId = group.Id;
            for (var i = 0; i < studentCount; i++)
            {
                students.Add((await TestData.AddMemberAsync(db, group)).Id);
            }

            for (var i = 0; i < teacherCount; i++)
            {
                teachers.Add((await TestData.AddMemberAsync(db, group, GroupRole.Teacher)).Id);
            }
        }

        var content = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title = "Labb",
            bodyMd = "",
            submissionModes = new[] { "file", "text" }
        });
        content.EnsureSuccessStatusCode();
        var contentId = (await content.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        // Eleverna läggs in före delningen, eftersom den skapar en inlämning per elev.
        var link = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", new { groupId });
        link.EnsureSuccessStatusCode();
        var linkId = (await link.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        return new FileAssignment(linkId, students, teachers);
    }

    private async Task<SubmissionResponse> SubmitFileAsync(Guid linkId, Guid student)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/links/{linkId}/submissions", student,
            FileForm("rapport.pdf"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!;
    }

    private async Task<Guid> AddTeacherOfNewGroupAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db);
        return (await TestData.AddMemberAsync(db, group, GroupRole.Teacher)).Id;
    }

    // Anropet görs som användaren i headern, så att behörigheten prövas mot just den användaren.
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, Guid asUser, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateContentAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/content",
            new { type = "theory", title = "Material", bodyMd = "" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static MultipartFormDataContent FileForm(string fileName, string? bodyMd = null, string field = "files")
    {
        var form = new MultipartFormDataContent();
        if (bodyMd is not null)
        {
            form.Add(new StringContent(bodyMd), "bodyMd");
        }

        form.Add(new ByteArrayContent([1, 2, 3]), field, fileName);
        return form;
    }
}
