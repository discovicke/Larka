using System.Net;
using System.Net.Http.Json;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Application.Services;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class SubmissionEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record IdResponse(Guid Id);

    private record AssignmentResponse(string GradeScale, List<string> SubmissionModes);

    private record ContentResponse(Guid Id, AssignmentResponse? Assignment);

    private record FileResponse(Guid Id, string FileName);

    private record RepoResponse(
        string RepoFullName,
        string CheckStatus,
        string ProvisioningState,
        string? ProvisioningError,
        string? SubmissionSha,
        DateTimeOffset? LockedAt,
        string? LockError);

    private record GradeResponse(string? Level, string Feedback);

    private record SubmissionResponse(
        Guid Id, Guid StudentId, string StudentName, string Status, List<FileResponse> Files, RepoResponse? Repo,
        GradeResponse? Grade);

    private static readonly object _fileSpec = new { submissionModes = new[] { "file" } };

    private static readonly object _gradedFileSpec =
        new { submissionModes = new[] { "file" }, gradeScale = "passFailDistinction" };


    [Fact]
    public async Task CreateAssignment_InRepoModeWithoutTemplate_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content",
            new { type = "assignment", title = "Labb", bodyMd = "", submissionModes = new[] { "repo" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNews_WithSubmissionMode_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content",
            new { type = "news", title = "Nyhet", bodyMd = "", submissionModes = new[] { "file" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LinkingFileAssignment_CreatesNotStartedSubmissionPerStudent()
    {
        var (groupId, _) = await AddGroupAsync(students: 2, teachers: 1);
        var assignment = await CreateAssignmentAsync(_gradedFileSpec);

        var linkId = await LinkAsync(assignment, new { groupId });

        var submissions = await ListAsync(linkId);
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, submission => Assert.Equal("NotStarted", submission.Status));
        Assert.All(submissions, submission => Assert.Null(submission.Repo));
        Assert.All(submissions, submission => Assert.Equal("Test Testsson", submission.StudentName));
    }

    [Fact]
    public async Task LinkingRepoAssignment_ToCourse_GivesEveryStudentAPendingRepo()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await CreateAssignmentAsync(
            new { submissionModes = new[] { "repo" }, templateRepo = "skol-org/labb-1-mall" });

        var linkId = await LinkAsync(assignment, new { courseId });

        var submissions = await ListAsync(linkId);
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, submission => Assert.Equal("Pending", submission.Repo!.ProvisioningState));
        Assert.All(submissions, submission => Assert.Equal("", submission.Repo!.RepoFullName));
    }

    [Fact]
    public async Task PendingRepo_IsProvisionedByBackgroundService()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await CreateAssignmentAsync(
            new { submissionModes = new[] { "repo" }, templateRepo = "skol-org/labb-1-mall" });
        var linkId = await LinkAsync(assignment, new { courseId });

        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<GitHubRepoProvisioningService>();
            Assert.True(await service.ProcessPendingAsync(CancellationToken.None) >= 2);
        }

        var submissions = await ListAsync(linkId);
        Assert.All(submissions, submission => Assert.Equal("Ready", submission.Repo!.ProvisioningState));
        Assert.All(submissions, submission => Assert.StartsWith("skol-org/labb-", submission.Repo!.RepoFullName));
    }

    [Fact]
    public async Task ReadyRepo_IsLockedAtDeadlineAndSavedAsSubmission()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await CreateAssignmentAsync(
            new
            {
                submissionModes = new[] { "repo" },
                templateRepo = "skol-org/labb-1-mall",
                lockAtDeadline = true
            });
        var linkId = await LinkAsync(assignment, new
        {
            courseId,
            dueAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<GitHubRepoProvisioningService>()
                .ProcessPendingAsync(CancellationToken.None);
            var locked = await scope.ServiceProvider
                .GetRequiredService<GitHubDeadlineLockService>()
                .ProcessDueAsync(CancellationToken.None);
            Assert.True(locked >= 2);
        }

        var submissions = await ListAsync(linkId);
        Assert.All(submissions, submission => Assert.Equal("Submitted", submission.Status));
        Assert.All(submissions, submission => Assert.NotNull(submission.Repo!.LockedAt));
        Assert.All(submissions, submission => Assert.Equal("fake-submission-sha", submission.Repo!.SubmissionSha));
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
        var linkId = await LinkAsync(await CreateAssignmentAsync(_fileSpec), new { groupId });

        var first = await SubmitFilesAsync(linkId, "rapport.pdf");
        var second = await SubmitFilesAsync(linkId, "rapport-v2.pdf");

        Assert.Equal(ResultStatus.Ok, first.Status);
        Assert.Equal(ResultStatus.Ok, second.Status);
        var submissions = await ListAsync(linkId);
        var submission = Assert.Single(submissions);
        Assert.Equal("Submitted", submission.Status);
        Assert.Equal(["rapport-v2.pdf"], submission.Files.Select(file => file.FileName));
    }

    [Fact]
    public async Task Resubmit_WithKeepFileIds_KeepsThoseFilesAndAddsNew()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = await LinkAsync(await CreateAssignmentAsync(_fileSpec), new { groupId });
        var first = await SubmitFilesAsync(linkId, "a.pdf", "b.pdf");
        var keep = first.Value!.Files.Single(file => file.FileName == "a.pdf").Id;

        var second = await SubmitFilesAsync(linkId, new List<Guid> { keep }, "c.pdf");

        Assert.Equal(ResultStatus.Ok, second.Status);
        var submission = Assert.Single(await ListAsync(linkId));
        Assert.Equal(["a.pdf", "c.pdf"], submission.Files.Select(file => file.FileName).Order());
    }

    [Fact]
    public async Task Resubmit_KeepingAllFiles_WithoutNewOnes_IsAllowed_ButKeepingNoneIsNot()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = await LinkAsync(await CreateAssignmentAsync(_fileSpec), new { groupId });
        var first = await SubmitFilesAsync(linkId, "a.pdf");
        var keep = first.Value!.Files.Single().Id;

        var kept = await SubmitFilesAsync(linkId, new List<Guid> { keep });
        var none = await SubmitFilesAsync(linkId, new List<Guid>());

        Assert.Equal(ResultStatus.Ok, kept.Status);
        Assert.Equal(ResultStatus.Invalid, none.Status);
        Assert.Single(Assert.Single(await ListAsync(linkId)).Files);
    }

    [Fact]
    public async Task Submit_FileAssignment_WithoutFiles_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = await LinkAsync(await CreateAssignmentAsync(_fileSpec), new { groupId });

        var response = await PostSubmissionAsync(linkId, "Glömde filen");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_ToNews_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var news = await _client.PostAsJsonAsync("/api/content", new { type = "news", title = "Nyhet", bodyMd = "" });
        var linkId = await LinkAsync((await news.Content.ReadFromJsonAsync<IdResponse>())!.Id, new { groupId });

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task NeedsWork_ThenResubmit_ThenGrade()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(_gradedFileSpec);
        var linkId = await LinkAsync(assignment, new { groupId });
        var submitted = await SubmitAsync(linkId);

        var needsWork = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { feedback = "Saknar diskussion", needsWork = true });
        Assert.Equal("NeedsWork", (await needsWork.Content.ReadFromJsonAsync<SubmissionResponse>())!.Status);

        await SubmitAsync(linkId);
        var graded = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "distinction", feedback = "Bra!" });
        var result = (await graded.Content.ReadFromJsonAsync<SubmissionResponse>())!;
        Assert.Equal("Graded", result.Status);
        Assert.Equal("Distinction", result.Grade!.Level);

        var afterGrade = await SubmitFilesAsync(linkId, "rapport.pdf");
        Assert.Equal(ResultStatus.Invalid, afterGrade.Status);
    }

    [Fact]
    public async Task Grade_PassFailScale_WithDistinction_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(
            new { submissionModes = new[] { "file" }, gradeScale = "passFail" });
        var linkId = await LinkAsync(assignment, new { groupId });
        var submitted = await SubmitAsync(linkId);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "distinction", feedback = "För bra" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_BeforeAnythingIsSubmitted_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 1);
        var assignment = await CreateAssignmentAsync(_gradedFileSpec);
        var linkId = await LinkAsync(assignment, new { groupId });
        var notStarted = Assert.Single(await ListAsync(linkId));

        var response = await _client.PostAsJsonAsync($"/api/submissions/{notStarted.Id}/grade",
            new { level = "pass", feedback = "?" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssignment_WithTemplateButNoRepoMode_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title = "Labb",
            bodyMd = "",
            submissionModes = new[] { "file" },
            templateRepo = "skol-org/mall"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssignment_WithSeveralModes_ReturnsThemAll()
    {
        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title = "Prov",
            bodyMd = "",
            gradeScale = "passFailDistinction",
            submissionModes = new[] { "text", "file", "onSite" }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
        Assert.Equal("PassFailDistinction", content.Assignment!.GradeScale);
        Assert.Equal(["Text", "File", "OnSite"], content.Assignment.SubmissionModes);
    }

    [Fact]
    public async Task Submit_TextAssignment_WithText_IsSubmitted()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(new { submissionModes = new[] { "text" } });
        var linkId = await LinkAsync(assignment, new { groupId });

        var response = await PostSubmissionAsync(linkId, "Mitt svar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Submit_TextAssignment_WithFiles_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(new { submissionModes = new[] { "text" } });
        var linkId = await LinkAsync(assignment, new { groupId });

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Submit_TextOrFileAssignment_AcceptsEither()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(new { submissionModes = new[] { "text", "file" } });
        var linkId = await LinkAsync(assignment, new { groupId });

        var withText = await PostSubmissionAsync(linkId, "Svar");
        var withFiles = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(HttpStatusCode.OK, withText.StatusCode);
        Assert.Equal(ResultStatus.Ok, withFiles.Status);
    }

    [Fact]
    public async Task Submit_OnSiteAssignment_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(new { submissionModes = new[] { "onSite" } });
        var linkId = await LinkAsync(assignment, new { groupId });

        var response = await PostSubmissionAsync(linkId, "Jag var där");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_OnSiteAssignment_WorksWithoutASubmission()
    {
        var (groupId, _) = await AddGroupAsync(students: 1);
        var assignment = await CreateAssignmentAsync(
            new { submissionModes = new[] { "onSite" }, gradeScale = "passFail" });
        var linkId = await LinkAsync(assignment, new { groupId });
        var notStarted = Assert.Single(await ListAsync(linkId));

        var response = await _client.PostAsJsonAsync($"/api/submissions/{notStarted.Id}/grade",
            new { level = "pass", feedback = "Bra redovisat" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var graded = (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!;
        Assert.Equal("Graded", graded.Status);
        Assert.Equal("Pass", graded.Grade!.Level);
    }

    [Fact]
    public async Task Grade_WithoutLevel_OnAGradedScale_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(
            new { submissionModes = new[] { "file" }, gradeScale = "passFail" });
        var submitted = await SubmitAsync(await LinkAsync(assignment, new { groupId }));

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { feedback = "Saknar betyg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_WithLevel_OnAnUngradedAssignment_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(_fileSpec);
        var submitted = await SubmitAsync(await LinkAsync(assignment, new { groupId }));

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "pass", feedback = "Inget betyg här" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_FeedbackOnly_OnAnUngradedAssignment_IsGraded()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await CreateAssignmentAsync(_fileSpec);
        var submitted = await SubmitAsync(await LinkAsync(assignment, new { groupId }));

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { feedback = "Bra jobbat" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Grade!.Level);
    }

    private async Task<Guid> CreateAssignmentAsync(object spec)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "assignment",
            ["title"] = "Labb 1",
            ["bodyMd"] = ""
        };
        foreach (var property in spec.GetType().GetProperties())
        {
            body[property.Name] = property.GetValue(spec);
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

    // Inlämningen skickas som multipart/form-data, precis som från formuläret, även när den bara har text.
    private async Task<HttpResponseMessage> PostSubmissionAsync(Guid linkId, string bodyMd)
    {
        var form = new MultipartFormDataContent { { new StringContent(bodyMd), "bodyMd" } };
        return await _client.PostAsync($"/api/links/{linkId}/submissions", form);
    }

    private async Task<List<SubmissionResponse>> ListAsync(Guid linkId) =>
        (await _client.GetFromJsonAsync<List<SubmissionResponse>>($"/api/links/{linkId}/submissions"))!;

    private async Task<SubmissionResponse> SubmitAsync(Guid linkId)
    {
        var result = await SubmitFilesAsync(linkId, "rapport.pdf");
        Assert.Equal(ResultStatus.Ok, result.Status);
        return (await _client.GetFromJsonAsync<SubmissionResponse>($"/api/submissions/{result.Value!.Id}"))!;
    }

    // Lämnas in genom servicen, så att testet inte beror på hur formuläret kodas (det täcks i FileUploadTests).
    private Task<ServiceResult<SubmissionDto>> SubmitFilesAsync(Guid linkId, params string[] fileNames) =>
        SubmitFilesAsync(linkId, null, fileNames);

    private async Task<ServiceResult<SubmissionDto>> SubmitFilesAsync(
        Guid linkId, List<Guid>? keepFileIds, params string[] fileNames)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SubmissionService>();
        var uploads = fileNames
            .Select(name => new FileUpload(name, 3, new MemoryStream([1, 2, 3])))
            .ToList();
        return await service.SubmitAsync(linkId, new SubmitDto(null, keepFileIds), uploads, CancellationToken.None);
    }

    private async Task<(Guid GroupId, Guid SchoolId)> AddGroupAsync(int students, int teachers = 0)
    {
        using var scope = factory.Services.CreateScope();
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
        using var scope = factory.Services.CreateScope();
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
