using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class SubmissionEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SubmissionEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record IdResponse(Guid Id);

    private record FileResponse(string FileName);

    private record RepoResponse(string RepoFullName, string CheckStatus);

    private record GradeResponse(int? Score, string Feedback);

    private record SubmissionResponse(
        Guid Id, Guid StudentId, string Status, List<FileResponse> Files, RepoResponse? Repo, GradeResponse? Grade);

    private static readonly object[] _oneFile = [new { fileName = "rapport.pdf", url = "https://files/rapport.pdf", sizeBytes = 1200 }];

    [Fact]
    public async Task CreateAssignment_InRepoModeWithoutTemplate_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content",
            new { type = "assignment", title = "Labb", bodyMd = "", submissionMode = "repo" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNews_WithSubmissionMode_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content",
            new { type = "news", title = "Nyhet", bodyMd = "", submissionMode = "file" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LinkingFileAssignment_CreatesNotStartedSubmissionPerStudent()
    {
        var (groupId, _) = await AddGroupAsync(students: 2, teachers: 1);
        var assignment = await CreateAssignmentAsync(new { submissionMode = "file", maxScore = 10 });

        var linkId = await LinkAsync(assignment, new { groupId });

        var submissions = await ListAsync(linkId);
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, s => Assert.Equal("NotStarted", s.Status));
        Assert.All(submissions, s => Assert.Null(s.Repo));
    }

    [Fact]
    public async Task LinkingRepoAssignment_ToCourse_GivesEveryStudentInReadingGroupsARepo()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await CreateAssignmentAsync(new { submissionMode = "repo", templateRepo = "skol-org/labb-1-mall" });

        var linkId = await LinkAsync(assignment, new { courseId });

        var submissions = await ListAsync(linkId);
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, s => Assert.Equal("Pending", s.Repo!.CheckStatus));
        Assert.All(submissions, s => Assert.StartsWith("skol-org/labb-", s.Repo!.RepoFullName));
    }

    [Fact]
    public async Task LinkingAssignmentWithoutSubmission_CreatesNoSubmissions()
    {
        var (groupId, _) = await AddGroupAsync(students: 2);
        var assignment = await CreateAssignmentAsync(new { });

        var linkId = await LinkAsync(assignment, new { groupId });

        Assert.Empty(await ListAsync(linkId));
    }

    [Fact]
    public async Task Submit_FileAssignment_WithFiles_IsSubmitted_AndResubmitKeepsOneRow()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = await LinkAsync(await CreateAssignmentAsync(new { submissionMode = "file" }), new { groupId });

        var first = await _client.PostAsJsonAsync($"/api/links/{linkId}/submissions", new { files = _oneFile });
        var second = await _client.PostAsJsonAsync($"/api/links/{linkId}/submissions", new
        {
            files = new[] { new { fileName = "rapport-v2.pdf", url = "https://files/v2.pdf", sizeBytes = 1300 } }
        });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var submissions = await ListAsync(linkId);
        var submission = Assert.Single(submissions);
        Assert.Equal("Submitted", submission.Status);
        Assert.Equal(["rapport-v2.pdf"], submission.Files.Select(f => f.FileName));
    }

    [Fact]
    public async Task Submit_FileAssignment_WithoutFiles_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = await LinkAsync(await CreateAssignmentAsync(new { submissionMode = "file" }), new { groupId });

        var response = await _client.PostAsJsonAsync($"/api/links/{linkId}/submissions", new { bodyMd = "Glömde filen" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_ToNews_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var news = await _client.PostAsJsonAsync("/api/content", new { type = "news", title = "Nyhet", bodyMd = "" });
        var linkId = await LinkAsync((await news.Content.ReadFromJsonAsync<IdResponse>())!.Id, new { groupId });

        var response = await _client.PostAsJsonAsync($"/api/links/{linkId}/submissions", new { files = _oneFile });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NeedsWork_ThenResubmit_ThenGrade()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = await LinkAsync(await CreateAssignmentAsync(new { submissionMode = "file", maxScore = 50 }), new { groupId });
        var submitted = await SubmitAsync(linkId);

        var needsWork = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { feedback = "Saknar diskussion", needsWork = true });
        Assert.Equal("NeedsWork", (await needsWork.Content.ReadFromJsonAsync<SubmissionResponse>())!.Status);

        await SubmitAsync(linkId);
        var graded = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { score = 40, feedback = "Bra!" });
        var result = (await graded.Content.ReadFromJsonAsync<SubmissionResponse>())!;
        Assert.Equal("Graded", result.Status);
        Assert.Equal(40, result.Grade!.Score);

        var afterGrade = await _client.PostAsJsonAsync($"/api/links/{linkId}/submissions", new { files = _oneFile });
        Assert.Equal(HttpStatusCode.BadRequest, afterGrade.StatusCode);
    }

    [Fact]
    public async Task Grade_AboveMaxScore_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = await LinkAsync(await CreateAssignmentAsync(new { submissionMode = "file", maxScore = 10 }), new { groupId });
        var submitted = await SubmitAsync(linkId);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { score = 11, feedback = "För bra" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_BeforeAnythingIsSubmitted_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 1);
        var linkId = await LinkAsync(await CreateAssignmentAsync(new { submissionMode = "file", maxScore = 10 }), new { groupId });
        var notStarted = Assert.Single(await ListAsync(linkId));

        var response = await _client.PostAsJsonAsync($"/api/submissions/{notStarted.Id}/grade",
            new { score = 5, feedback = "?" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> CreateAssignmentAsync(object spec)
    {
        var body = new Dictionary<string, object?> { ["type"] = "assignment", ["title"] = "Labb 1", ["bodyMd"] = "" };
        foreach (var p in spec.GetType().GetProperties())
        {
            body[p.Name] = p.GetValue(spec);
        }

        var response = await _client.PostAsJsonAsync("/api/content", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<Guid> LinkAsync(Guid contentId, object link)
    {
        var response = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", link);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private async Task<List<SubmissionResponse>> ListAsync(Guid linkId) =>
        (await _client.GetFromJsonAsync<List<SubmissionResponse>>($"/api/links/{linkId}/submissions"))!;

    private async Task<SubmissionResponse> SubmitAsync(Guid linkId)
    {
        var response = await _client.PostAsJsonAsync($"/api/links/{linkId}/submissions", new { files = _oneFile });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!;
    }

    private async Task<(Guid GroupId, Guid SchoolId)> AddGroupAsync(int students, int teachers = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db);
        for (var i = 0; i < students; i++)
        {
            await TestData.AddMemberAsync(db, group);
        }

        for (var i = 0; i < teachers; i++)
        {
            await TestData.AddMemberAsync(db, group, GroupRole.Teacher);
        }

        return (group.Id, group.SchoolId);
    }

    private async Task<Guid> AddCourseReadByTwoGroupsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var groupA = await TestData.AddGroupAsync(db);
        var groupB = await TestData.AddGroupAsync(db, groupA.SchoolId);
        await TestData.AddMemberAsync(db, groupA);
        await TestData.AddMemberAsync(db, groupB);
        var course = await TestData.AddCourseAsync(db, groupA.SchoolId, groupA.Id);
        db.GroupCourses.Add(new Backend.Domain.Entities.GroupCourse { GroupId = groupB.Id, CourseId = course.Id });
        await db.SaveChangesAsync();
        return course.Id;
    }
}
