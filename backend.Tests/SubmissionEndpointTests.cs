using System.Net;
using System.Net.Http.Json;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Application.Services;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class SubmissionEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record CreatedWithLink(LinkResponse Link);

    private record AssignmentResponse(string GradeScale, List<string> SubmissionModes);

    private record ContentResponse(Guid Id, AssignmentResponse? Assignment);

    private record FileResponse(Guid Id, string FileName);

    private record RepoResponse(
        string RepoFullName,
        string? LastSha,
        string CheckStatus,
        string ProvisioningState,
        string? ProvisioningError,
        string? SubmissionSha,
        DateTimeOffset? LockedAt,
        string? LockError,
        DateTimeOffset? ReopenedAt = null,
        string? LastCommitMessage = null,
        DateTimeOffset? LastCommitAt = null,
        int CommitCount = 0,
        int ChecksPassed = 0,
        int ChecksTotal = 0);

    private record GradeResponse(string? Level, string Feedback, string? GradedByName = null);

    private record SupportResponse(
        bool NotStarted,
        bool NoRecentPush,
        bool RedChecks,
        bool HelpRequested,
        string? HelpMessage);

    private record SubmissionResponse(
        Guid Id,
        Guid StudentId,
        string StudentName,
        string Status,
        List<FileResponse> Files,
        RepoResponse? Repo,
        GradeResponse? Grade,
        SupportResponse? Support = null);

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
            new { type = "news", title = "Nyhet", bodyMd = "Text", submissionModes = new[] { "file" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LinkingFileAssignment_CreatesNotStartedSubmissionPerStudent()
    {
        var (groupId, _) = await AddGroupAsync(students: 2, teachers: 1);
        var assignment = await _client.CreateAssignmentAsync(_gradedFileSpec);

        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;

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
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "repo" }, templateRepo = "skol-org/labb-1-mall" });

        var linkId = (await _client.CreateLinkAsync(assignment, new { courseId })).Id;

        var submissions = await ListAsync(linkId);
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, submission => Assert.Equal("Pending", submission.Repo!.ProvisioningState));
        Assert.All(submissions, submission => Assert.Equal("", submission.Repo!.RepoFullName));
    }

    [Fact]
    public async Task PendingRepo_IsProvisionedByBackgroundService()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "repo" }, templateRepo = "skol-org/labb-1-mall" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { courseId })).Id;

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
    public async Task RepoActions_RefreshAndLockOnlyTheSelectedAssignment()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "repo" }, templateRepo = "skol-org/labb-1-mall" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { courseId })).Id;

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<GitHubRepoProvisioningService>()
                .ProcessPendingAsync(CancellationToken.None);
        }

        var refresh = await _client.PostAsync($"/api/links/{linkId}/repos/refresh", null);
        Assert.Equal(HttpStatusCode.NoContent, refresh.StatusCode);

        var lockResponse = await _client.PostAsync($"/api/links/{linkId}/repos/lock", null);
        Assert.Equal(HttpStatusCode.NoContent, lockResponse.StatusCode);

        var locked = await ListAsync(linkId);
        Assert.All(locked, submission =>
        {
            // Repona är orörda, så de låses men räknas inte som inlämnade.
            Assert.Equal("NotStarted", submission.Status);
            Assert.NotNull(submission.Repo!.LockedAt);
        });
    }

    [Fact]
    public async Task SupportFlags_DetectNotStartedOldPushAndRedChecks()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "repo" }, templateRepo = "skol-org/labb-1-mall" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { courseId })).Id;

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<GitHubRepoProvisioningService>()
                .ProcessPendingAsync(CancellationToken.None);

            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var now = DateTimeOffset.UtcNow;
            var submissionIds = db.Submissions
                .Where(submission => submission.ContentLinkId == linkId)
                .Select(submission => submission.Id);
            foreach (var repo in db.StudentRepos.Where(item => submissionIds.Contains(item.SubmissionId)))
            {
                repo.InitialSha = "initial-sha";
                repo.LastSha = "initial-sha";
                repo.CommitCount = 1;
                repo.ProvisionedAt = now.AddDays(-4);
                repo.LastCommitAt = now.AddDays(-5);
            }

            await db.SaveChangesAsync();
        }

        var notStarted = (await ListAsync(linkId))[0];
        Assert.True(notStarted.Support?.NotStarted);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var now = DateTimeOffset.UtcNow;
            var submissionIds = db.Submissions
                .Where(submission => submission.ContentLinkId == linkId)
                .Select(submission => submission.Id);
            foreach (var repo in db.StudentRepos.Where(item => submissionIds.Contains(item.SubmissionId)))
            {
                repo.LastSha = "student-sha";
                repo.CommitCount = 2;
                repo.LastCommitAt = now.AddDays(-5);
                repo.CheckStatus = CheckStatus.Red;
                repo.CheckStatusChangedAt = now.AddHours(-40);
            }

            await db.SaveChangesAsync();
        }

        var started = (await ListAsync(linkId))[0];
        Assert.False(started.Support?.NotStarted);
        Assert.True(started.Support?.NoRecentPush);
        Assert.True(started.Support?.RedChecks);
    }

    [Fact]
    public async Task RequestingMoreWork_ReopensTheRepoForResubmission()
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "repo" }, templateRepo = "skol-org/labb-1-mall" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { courseId })).Id;

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<GitHubRepoProvisioningService>()
                .ProcessPendingAsync(CancellationToken.None);
        }

        var submission = (await ListAsync(linkId))[0];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var entity = await db.Submissions.FindAsync(submission.Id);
            entity!.Status = SubmissionStatus.Submitted;
            entity.SubmittedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<SubmissionService>();
            var graded = await service.GradeAsync(
                submission.Id,
                new GradeSubmissionDto("Komplettera releaseflödet.", null, GradeOutcome.NeedsWork),
                CancellationToken.None);
            Assert.True(graded.Status == ResultStatus.Ok, graded.Error);
        }

        var updated = (await ListAsync(linkId)).Single(item => item.Id == submission.Id);
        Assert.NotNull(updated.Repo!.ReopenedAt);
    }

    // --- Repolåset följer deadlinerna ---

    // En utdelning i repo-läge med två elever. Repona provisioneras och får mallens commit som startpunkt,
    // och deadlinerna sätts efteråt. UpdatedAt flyttas bakåt eftersom ett riktigt repo skapas långt före deadline.
    private async Task<(Guid LinkId, List<Guid> RepoSubmissionIds)> RepoLinkAsync(
        DateTimeOffset? dueAt, params DateTimeOffset[] resubmitDueAt)
    {
        var courseId = await AddCourseReadByTwoGroupsAsync();
        var assignment = await _client.CreateAssignmentAsync(
            new
            {
                submissionModes = new[] { "repo" },
                templateRepo = "skol-org/labb-1-mall",
                lockAtDeadline = true
            });
        var linkId = (await _client.CreateLinkAsync(assignment, new { courseId })).Id;
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<GitHubRepoProvisioningService>()
                .ProcessPendingAsync(CancellationToken.None);
        }

        await SetDeadlinesAsync(linkId, dueAt, resubmitDueAt);
        await UpdateReposAsync(linkId, (_, repo) => repo.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-30));
        var ids = (await ListAsync(linkId)).Select(item => item.Id).ToList();
        return (linkId, ids);
    }

    private async Task UpdateReposAsync(
        Guid linkId, Action<Guid, Backend.Domain.Entities.StudentRepo> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submissionIds = db.Submissions
            .Where(submission => submission.ContentLinkId == linkId)
            .Select(submission => submission.Id);
        foreach (var repo in db.StudentRepos.Where(item => submissionIds.Contains(item.SubmissionId)))
        {
            change(repo.SubmissionId, repo);
        }

        await db.SaveChangesAsync();
    }

    private async Task RunLockJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<GitHubDeadlineLockService>()
            .ProcessDueAsync(CancellationToken.None);
    }

    private async Task<SubmissionResponse> RepoOfAsync(Guid linkId, Guid submissionId) =>
        (await ListAsync(linkId)).Single(item => item.Id == submissionId);

    [Fact]
    public async Task UntouchedRepo_AtTheMainDeadline_StaysOpenAndNotStarted()
    {
        var (linkId, ids) = await RepoLinkAsync(DateTimeOffset.UtcNow.AddMinutes(-1), _future);
        await UpdateReposAsync(linkId, (id, repo) =>
        {
            if (id == ids[0])
            {
                repo.LastSha = "student-sha";
                repo.CommitCount = 3;
            }
        });

        await RunLockJobAsync();

        var touched = await RepoOfAsync(linkId, ids[0]);
        Assert.Equal("Submitted", touched.Status);
        Assert.NotNull(touched.Repo!.LockedAt);
        Assert.Equal("student-sha", touched.Repo.SubmissionSha);
        var untouched = await RepoOfAsync(linkId, ids[1]);
        Assert.Equal("NotStarted", untouched.Status);
        Assert.Null(untouched.Repo!.LockedAt);
        Assert.Null(untouched.Repo.SubmissionSha);
    }

    [Fact]
    public async Task UntouchedRepo_IsNotLockedByLaterRuns_ButLocksAtTheNextDeadline()
    {
        var (linkId, ids) = await RepoLinkAsync(DateTimeOffset.UtcNow.AddMinutes(-1), _future);
        await RunLockJobAsync();

        // En commit efter deadlinen räknas inte förrän nästa deadline, så samma deadline tas inte om.
        await UpdateReposAsync(linkId, (_, repo) => repo.LastSha = "late-sha");
        await RunLockJobAsync();
        var afterLateCommit = await RepoOfAsync(linkId, ids[1]);
        Assert.Null(afterLateCommit.Repo!.LockedAt);
        Assert.Equal("NotStarted", afterLateCommit.Status);

        // Nästa deadline passerar en timme efter förra behandlingen: nu räknas committen.
        await UpdateReposAsync(linkId, (_, repo) => repo.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1));
        await SetDeadlinesAsync(
            linkId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMinutes(-1), _future);
        await RunLockJobAsync();
        var locked = await RepoOfAsync(linkId, ids[1]);
        Assert.Equal("Submitted", locked.Status);
        Assert.Equal("late-sha", locked.Repo!.SubmissionSha);
    }

    [Fact]
    public async Task ReopenedRepo_WithoutNewCommit_StaysOpenAtTheResubmitDeadline()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddMinutes(-1), _future);
        await ReopenAsync(linkId, ids, "handed-in-sha", "handed-in-sha");

        await RunLockJobAsync();

        var reopened = await RepoOfAsync(linkId, ids[0]);
        Assert.Equal("NeedsWork", reopened.Status);
        Assert.NotNull(reopened.Repo!.ReopenedAt);
        Assert.Equal("handed-in-sha", reopened.Repo.SubmissionSha);
    }

    [Fact]
    public async Task ReopenedRepo_WithNewCommit_LocksAtTheResubmitDeadline()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddMinutes(-1), _future);
        await ReopenAsync(linkId, ids, "handed-in-sha", "fixed-sha");

        await RunLockJobAsync();

        var locked = await RepoOfAsync(linkId, ids[0]);
        Assert.Equal("Submitted", locked.Status);
        Assert.Null(locked.Repo!.ReopenedAt);
        Assert.Equal("fixed-sha", locked.Repo.SubmissionSha);
    }

    [Fact]
    public async Task ReopenedRepo_IsNotLockedAgainBeforeTheNextDeadlinePasses()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddHours(-1), _future);
        await ReopenAsync(linkId, ids, "handed-in-sha", "fixed-sha");
        // Läraren öppnade repot efter att komplettering 1 redan passerat.
        var reopenedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await UpdateReposAsync(linkId, (_, repo) =>
        {
            repo.UpdatedAt = reopenedAt;
            repo.ReopenedAt = reopenedAt;
        });

        await RunLockJobAsync();

        var reopened = await RepoOfAsync(linkId, ids[0]);
        Assert.Equal("NeedsWork", reopened.Status);
        Assert.NotNull(reopened.Repo!.ReopenedAt);
    }

    private async Task SetPersonalDueAtDirectAsync(Guid submissionId, DateTimeOffset? personal)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submission = await db.Submissions.FindAsync(submissionId);
        submission!.PersonalDueAt = personal;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PersonalDueAt_InTheFuture_KeepsAnUntouchedRepoOpenAfterTheClassDeadlines()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddMinutes(-1));
        await SetPersonalDueAtDirectAsync(ids[0], DateTimeOffset.UtcNow.AddDays(2));

        await RunLockJobAsync();

        Assert.Null((await RepoOfAsync(linkId, ids[0])).Repo!.LockedAt);
        Assert.NotNull((await RepoOfAsync(linkId, ids[1])).Repo!.LockedAt);
    }

    [Fact]
    public async Task PersonalDueAt_WhenItHasPassed_LocksTheRepo()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        await SetPersonalDueAtDirectAsync(ids[0], DateTimeOffset.UtcNow.AddDays(2));
        await RunLockJobAsync();
        Assert.Null((await RepoOfAsync(linkId, ids[0])).Repo!.LockedAt);

        // Den personliga deadlinen passerar efter förra behandlingen: nu låses repot.
        await SetPersonalDueAtDirectAsync(ids[0], DateTimeOffset.UtcNow.AddMinutes(-1));
        await UpdateReposAsync(linkId, (_, repo) => repo.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1));
        await RunLockJobAsync();

        Assert.NotNull((await RepoOfAsync(linkId, ids[0])).Repo!.LockedAt);
    }

    [Fact]
    public async Task SettingAPersonalDueAt_ReopensALockedRepoThatHasNotBeenHandedIn()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        await RunLockJobAsync();
        Assert.NotNull((await RepoOfAsync(linkId, ids[0])).Repo!.LockedAt);
        var studentId = await StudentOfAsync(ids[0]);

        var response = await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddDays(2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull((await RepoOfAsync(linkId, ids[0])).Repo!.ReopenedAt);
        // En elev som redan lämnat in får inte repot öppnat.
        var submittedId = ids[1];
        await SetStatusAsync(submittedId, SubmissionStatus.Submitted);
        var other = await StudentOfAsync(submittedId);
        await PutPersonalDueAtAsync(linkId, other, DateTimeOffset.UtcNow.AddDays(2));
        Assert.Null((await RepoOfAsync(linkId, submittedId)).Repo!.ReopenedAt);
    }

    [Fact]
    public async Task GroupRepo_FollowsTheLatestPersonalDueAtAmongTheMembers()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        var groupId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.AssignmentGroups.Add(new Backend.Domain.Entities.AssignmentGroup
            {
                Id = groupId,
                ContentLinkId = linkId,
                Name = "Grupp 1"
            });
            await db.SaveChangesAsync();
            foreach (var id in ids)
            {
                (await db.Submissions.FindAsync(id))!.AssignmentGroupId = groupId;
            }

            // Repot hänger på första medlemmens inlämning; den andra raden har inget eget repo.
            db.StudentRepos.Remove(await db.StudentRepos.SingleAsync(repo => repo.SubmissionId == ids[1]));
            (await db.StudentRepos.SingleAsync(repo => repo.SubmissionId == ids[0])).AssignmentGroupId = groupId;
            await db.SaveChangesAsync();
        }

        // Första medlemmen har ingen egen deadline, men den andra har en: repot väntar på den senare.
        await SetPersonalDueAtDirectAsync(ids[1], DateTimeOffset.UtcNow.AddDays(2));
        await RunLockJobAsync();
        Assert.Null((await RepoOfAsync(linkId, ids[0])).Repo!.LockedAt);

        await SetPersonalDueAtDirectAsync(ids[1], DateTimeOffset.UtcNow.AddMinutes(-1));
        await UpdateReposAsync(linkId, (_, repo) => repo.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1));
        await RunLockJobAsync();
        Assert.NotNull((await RepoOfAsync(linkId, ids[0])).Repo!.LockedAt);
    }

    private async Task<Guid> StudentOfAsync(Guid submissionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await db.Submissions.AsNoTracking().SingleAsync(row => row.Id == submissionId)).StudentId;
    }

    // Första eleven har lämnat in, fått komplettera och repot är öppnat igen. LastSha är senaste commit.
    private async Task ReopenAsync(Guid linkId, List<Guid> ids, string submissionSha, string lastSha)
    {
        await SetStatusAsync(ids[0], SubmissionStatus.NeedsWork);
        await UpdateReposAsync(linkId, (id, repo) =>
        {
            if (id != ids[0])
            {
                return;
            }

            repo.LockedAt = DateTimeOffset.UtcNow.AddDays(-9);
            repo.SubmissionSha = submissionSha;
            repo.ReopenedAt = DateTimeOffset.UtcNow.AddDays(-8);
            repo.UpdatedAt = repo.ReopenedAt.Value;
            repo.LastSha = lastSha;
            repo.CommitCount = 3;
        });
    }

    [Fact]
    public async Task AfterTheLastDeadline_EveryRepoIsLocked_WithoutChangingTheStatusOfUntouchedOnes()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddMinutes(-1));

        await RunLockJobAsync();

        foreach (var id in ids)
        {
            var locked = await RepoOfAsync(linkId, id);
            Assert.NotNull(locked.Repo!.LockedAt);
            Assert.Equal("NotStarted", locked.Status);
            Assert.Null(locked.Repo.SubmissionSha);
        }
    }

    [Fact]
    public async Task RepoCreatedAfterTheOnlyDeadline_IsLockedOnTheNextRun_AndNotSelectedAgain()
    {
        var (linkId, ids) = await RepoLinkAsync(DateTimeOffset.UtcNow.AddHours(-2));
        await UpdateReposAsync(linkId, (_, repo) => repo.UpdatedAt = DateTimeOffset.UtcNow);

        await RunLockJobAsync();

        foreach (var id in ids)
        {
            var locked = await RepoOfAsync(linkId, id);
            Assert.NotNull(locked.Repo!.LockedAt);
            Assert.Equal("NotStarted", locked.Status);
        }

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<Backend.Application.Abstractions.ISubmissionRepository>();
        var due = await repository.GetReposToLockAsync(DateTimeOffset.UtcNow, 1000, CancellationToken.None);
        Assert.DoesNotContain(due, context => ids.Contains(context.Submission.Id));
    }

    [Fact]
    public async Task RepoCreatedBetweenDeadlines_IsLeftAloneUntilTheResubmitDeadlinePasses()
    {
        var (linkId, ids) = await RepoLinkAsync(
            DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMinutes(-10), _future);
        await UpdateReposAsync(linkId, (_, repo) => repo.UpdatedAt = DateTimeOffset.UtcNow);

        await RunLockJobAsync();

        foreach (var id in ids)
        {
            Assert.Null((await RepoOfAsync(linkId, id)).Repo!.LockedAt);
        }
    }

    [Fact]
    public async Task ManualLock_ForRepoCreatedAfterTheOnlyDeadline_LocksIt()
    {
        var (linkId, ids) = await RepoLinkAsync(DateTimeOffset.UtcNow.AddHours(-2));
        await UpdateReposAsync(linkId, (_, repo) => repo.UpdatedAt = DateTimeOffset.UtcNow);

        var response = await _client.PostAsync($"/api/links/{linkId}/repos/lock", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        foreach (var id in ids)
        {
            Assert.NotNull((await RepoOfAsync(linkId, id)).Repo!.LockedAt);
        }
    }

    [Fact]
    public async Task GradedSubmission_IsLockedAtTheDeadline_AndStaysGraded()
    {
        var (linkId, ids) = await RepoLinkAsync(DateTimeOffset.UtcNow.AddMinutes(-1), _future);
        await SetStatusAsync(ids[0], SubmissionStatus.Graded);
        await UpdateReposAsync(linkId, (_, repo) => repo.LastSha = "student-sha");

        await RunLockJobAsync();

        var graded = await RepoOfAsync(linkId, ids[0]);
        Assert.Equal("Graded", graded.Status);
        Assert.NotNull(graded.Repo!.LockedAt);
    }

    [Fact]
    public async Task ManualLock_WithoutDueAt_LocksEveryRepo_AndOnlyOwnCommitsAreSubmitted()
    {
        var (linkId, ids) = await RepoLinkAsync(null);
        await UpdateReposAsync(linkId, (id, repo) =>
        {
            if (id == ids[0])
            {
                repo.LastSha = "student-sha";
                repo.CommitCount = 2;
            }
        });

        var response = await _client.PostAsync($"/api/links/{linkId}/repos/lock", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var touched = await RepoOfAsync(linkId, ids[0]);
        Assert.Equal("Submitted", touched.Status);
        Assert.Equal("student-sha", touched.Repo!.SubmissionSha);
        var untouched = await RepoOfAsync(linkId, ids[1]);
        Assert.Equal("NotStarted", untouched.Status);
        Assert.NotNull(untouched.Repo!.LockedAt);
    }

    [Fact]
    public async Task LinkingAssignmentWithoutSubmission_CreatesNoSubmissions()
    {
        var (groupId, _) = await AddGroupAsync(students: 2);
        var assignment = await _client.CreateAssignmentAsync(new { });

        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;

        Assert.Empty(await ListAsync(linkId));
    }

    [Fact]
    public async Task Submit_FileAssignment_WithFiles_IsSubmitted_AndResubmitKeepsOneRow()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var linkId = (await _client.CreateLinkAsync(await _client.CreateAssignmentAsync(_fileSpec), new { groupId }))
            .Id;

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
        var linkId = (await _client.CreateLinkAsync(await _client.CreateAssignmentAsync(_fileSpec), new { groupId }))
            .Id;
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
        var linkId = (await _client.CreateLinkAsync(await _client.CreateAssignmentAsync(_fileSpec), new { groupId }))
            .Id;
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
        var linkId = (await _client.CreateLinkAsync(await _client.CreateAssignmentAsync(_fileSpec), new { groupId }))
            .Id;

        var response = await PostSubmissionAsync(linkId, "Glömde filen");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_ToNews_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var news = await _client.PostAsJsonAsync(
            "/api/content", new { type = "news", title = "Nyhet", bodyMd = "Text" });
        var linkId = (await _client.CreateLinkAsync(
            (await news.Content.ReadFromJsonAsync<IdResponse>())!.Id,
            new { groupId })).Id;

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task NeedsWork_ThenResubmit_ThenGrade()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(_gradedFileSpec);
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        var submitted = await SubmitAsync(linkId);

        var needsWork = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { feedback = "Saknar diskussion", outcome = "needsWork" });
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
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "file" }, gradeScale = "passFail" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        var submitted = await SubmitAsync(linkId);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "distinction", feedback = "För bra" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_BeforeAnythingIsSubmitted_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 1);
        var assignment = await _client.CreateAssignmentAsync(_gradedFileSpec);
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
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
        var assignment = await _client.CreateAssignmentAsync(new { submissionModes = new[] { "text" } });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;

        var response = await PostSubmissionAsync(linkId, "Mitt svar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Submit_TextLongerThanTheLimit_ReturnsBadRequest_AndSavesNothing()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(new { submissionModes = new[] { "text" } });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;

        var response = await PostSubmissionAsync(linkId, new string('a', 50_001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("the answer can be at most 50000 characters", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(await ListAsync(linkId), submission => submission.Status == "Submitted");
    }

    [Fact]
    public async Task Submit_TextAssignment_WithFiles_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(new { submissionModes = new[] { "text" } });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Submit_TextOrFileAssignment_AcceptsEither()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(new { submissionModes = new[] { "text", "file" } });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;

        var withText = await PostSubmissionAsync(linkId, "Svar");
        var withFiles = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(HttpStatusCode.OK, withText.StatusCode);
        Assert.Equal(ResultStatus.Ok, withFiles.Status);
    }

    [Fact]
    public async Task Submit_OnSiteAssignment_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(new { submissionModes = new[] { "onSite" } });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;

        var response = await PostSubmissionAsync(linkId, "Jag var där");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_OnSiteAssignment_WorksWithoutASubmission()
    {
        var (groupId, _) = await AddGroupAsync(students: 1);
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "onSite" }, gradeScale = "passFail" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        var notStarted = Assert.Single(await ListAsync(linkId));

        var response = await _client.PostAsJsonAsync($"/api/submissions/{notStarted.Id}/grade",
            new { level = "pass", feedback = "Bra redovisat" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var graded = (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!;
        Assert.Equal("Graded", graded.Status);
        Assert.Equal("Pass", graded.Grade!.Level);
    }

    [Fact]
    public async Task Grade_Fail_WithoutComment_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "file" }, gradeScale = "passFail" });
        var submitted = await SubmitAsync((await _client.CreateLinkAsync(assignment, new { groupId })).Id);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "fail", feedback = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_Fail_WithComment_IsGraded()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "file" }, gradeScale = "passFail" });
        var submitted = await SubmitAsync((await _client.CreateLinkAsync(assignment, new { groupId })).Id);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "fail", feedback = "Testerna saknas." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Fail", (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Grade!.Level);
    }

    [Fact]
    public async Task Grade_TellsWhoGradedIt_InTheGradeAndInTheList()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "file" }, gradeScale = "passFail" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        var submitted = await SubmitAsync(linkId);
        string graderName;
        using (var scope = factory.Services.CreateScope())
        {
            graderName = (await scope.ServiceProvider.GetRequiredService<AppDb>().Users
                .SingleAsync(user => user.Id == PlaceholderIds.UserId)).Name;
        }

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "pass", feedback = "Bra." });

        Assert.Equal(graderName, (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Grade!.GradedByName);
        Assert.Equal(graderName, (await ListAsync(linkId)).Single().Grade!.GradedByName);
    }

    [Fact]
    public async Task Grade_WithoutLevel_OnAGradedScale_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "file" }, gradeScale = "passFail" });
        var submitted = await SubmitAsync((await _client.CreateLinkAsync(assignment, new { groupId })).Id);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { feedback = "Saknar betyg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_WithLevel_OnAnUngradedAssignment_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(_fileSpec);
        var submitted = await SubmitAsync((await _client.CreateLinkAsync(assignment, new { groupId })).Id);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { level = "pass", feedback = "Inget betyg här" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_FeedbackOnly_OnAnUngradedAssignment_IsGraded()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(_fileSpec);
        var submitted = await SubmitAsync((await _client.CreateLinkAsync(assignment, new { groupId })).Id);

        var response = await _client.PostAsJsonAsync($"/api/submissions/{submitted.Id}/grade",
            new { feedback = "Bra jobbat" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Grade!.Level);
    }

    // --- Inlämning följer deadlinerna ---

    private static readonly DateTimeOffset _past = DateTimeOffset.UtcNow.AddDays(-10);
    private static readonly DateTimeOffset _future = DateTimeOffset.UtcNow.AddDays(10);

    // Sätter deadlinerna direkt, eftersom tiden inte går att flytta i testet.
    private async Task SetDeadlinesAsync(Guid linkId, DateTimeOffset? dueAt, params DateTimeOffset[] resubmitDueAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var link = await db.ContentLinks.FindAsync(linkId);
        link!.DueAt = dueAt;
        link.ResubmitDueAt = [.. resubmitDueAt];
        await db.SaveChangesAsync();
    }

    private async Task SetStatusAsync(Guid submissionId, SubmissionStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submission = await db.Submissions.FindAsync(submissionId);
        submission!.Status = status;
        await db.SaveChangesAsync();
    }

    // En länk där eleven redan har en inlämning med given status och deadlinerna satta.
    private async Task<(Guid LinkId, Guid SubmissionId)> LinkWithStatusAsync(
        SubmissionStatus status, DateTimeOffset? dueAt, params DateTimeOffset[] resubmitDueAt)
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(_gradedFileSpec);
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        var submitted = await SubmitAsync(linkId);
        await SetStatusAsync(submitted.Id, status);
        await SetDeadlinesAsync(linkId, dueAt, resubmitDueAt);
        return (linkId, submitted.Id);
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.Submitted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public async Task Submit_BeforeTheMainDeadline_IsAllowedForEveryStatusButGraded(SubmissionStatus status)
    {
        var (linkId, _) = await LinkWithStatusAsync(status, _future, _future.AddDays(7));

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal(SubmissionStatus.Submitted, result.Value!.Status);
    }

    [Fact]
    public async Task Submit_OnAGradedSubmission_IsRefused()
    {
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.Graded, _future);

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted, true)]
    [InlineData(SubmissionStatus.NeedsWork, true)]
    [InlineData(SubmissionStatus.Submitted, false)]
    public async Task Submit_BetweenDeadlines_OnlyTheOnesWhoCanResubmitMay(SubmissionStatus status, bool allowed)
    {
        var (linkId, _) = await LinkWithStatusAsync(status, _past, _future);

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(allowed, result.Status == ResultStatus.Ok);
        if (!allowed)
        {
            Assert.Equal(GradingWindow.LockedMessage, result.Error);
        }
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.Submitted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public async Task Submit_AfterTheLastDeadline_IsRefused(SubmissionStatus status)
    {
        var (linkId, _) = await LinkWithStatusAsync(status, _past.AddDays(-5), _past);

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Submit_AfterTheMainDeadline_WithoutResubmitDeadlines_IsRefused()
    {
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.NeedsWork, _past);

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(GradingWindow.ClosedMessage, result.Error);
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.Submitted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public async Task Submit_WithoutADueDate_IsAlwaysOpenUntilGraded(SubmissionStatus status)
    {
        var (linkId, _) = await LinkWithStatusAsync(status, null);

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task Submit_WithNoSubmissionRow_AfterTheLastDeadline_IsRefused()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(_gradedFileSpec);
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        await SetDeadlinesAsync(linkId, _past);

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    // --- IG och Komplettera följer deadlinerna ---

    private async Task<HttpResponseMessage> GradeAsync(Guid submissionId, object body) =>
        await _client.PostAsJsonAsync($"/api/submissions/{submissionId}/grade", body);

    [Fact]
    public async Task Grade_NeedsWork_AfterTheLastDeadline_GivesTheStudentANewDeadlineInTheSameStep()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past);
        var personal = DateTimeOffset.UtcNow.AddDays(5);

        var withoutDate = await GradeAsync(submissionId, new { feedback = "Lägg till tester", outcome = "needsWork" });
        var withDate = await GradeAsync(
            submissionId, new { feedback = "Lägg till tester", outcome = "needsWork", personalDueAt = personal });

        Assert.Equal(HttpStatusCode.BadRequest, withoutDate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withDate.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var saved = await db.Submissions.AsNoTracking().SingleAsync(row => row.Id == submissionId);
        Assert.Equal(SubmissionStatus.NeedsWork, saved.Status);
        Assert.Equal(personal.UtcDateTime, saved.PersonalDueAt!.Value.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Grade_WithANewDeadline_OnlyWorksForNeedsWork_AndAfterTheClassDeadlines()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past, _future);

        var graded = await GradeAsync(submissionId, new
        {
            feedback = "Bra",
            level = "pass",
            personalDueAt = _future.AddDays(2)
        });
        var tooEarly = await GradeAsync(submissionId, new
        {
            feedback = "Mer",
            outcome = "needsWork",
            personalDueAt = DateTimeOffset.UtcNow.AddDays(2)
        });

        Assert.Equal(HttpStatusCode.BadRequest, graded.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
    }

    [Fact]
    public async Task Grade_Fail_WhileADeadlineRemains_IsRefused()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _future);

        var response = await GradeAsync(submissionId, new { level = "fail", feedback = "Saknas." });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("IG", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Grade_Fail_BetweenDeadlines_IsRefused()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past, _future);

        var response = await GradeAsync(submissionId, new { level = "fail", feedback = "Saknas." });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grade_Fail_AfterTheLastDeadline_IsAllowed()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past.AddDays(-5), _past);

        var response = await GradeAsync(submissionId, new { level = "fail", feedback = "Saknas." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Fail", (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Grade!.Level);
    }

    [Fact]
    public async Task Grade_NeedsWork_WhileADeadlineRemains_IsAllowed()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past, _future);

        var response = await GradeAsync(submissionId, new { feedback = "Komplettera", outcome = "needsWork" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("NeedsWork", (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Status);
    }

    [Fact]
    public async Task Grade_NeedsWork_AfterTheLastDeadline_IsRefused()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past.AddDays(-5), _past);

        var response = await GradeAsync(submissionId, new { feedback = "Komplettera", outcome = "needsWork" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Komplettera", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("pass")]
    [InlineData("distinction")]
    public async Task Grade_PassOrDistinction_IsAllowedBeforeAndAfterTheDeadlines(string level)
    {
        var (_, before) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _future);
        var (_, after) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past);

        Assert.Equal(HttpStatusCode.OK, (await GradeAsync(before, new { level, feedback = "Bra" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GradeAsync(after, new { level, feedback = "Bra" })).StatusCode);
    }

    [Fact]
    public async Task Grade_WithoutADueDate_AllowsEveryOutcome()
    {
        var (_, failId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, null);
        var (_, workId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, null);

        Assert.Equal(
            HttpStatusCode.OK,
            (await GradeAsync(failId, new { level = "fail", feedback = "Saknas." })).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await GradeAsync(workId, new { feedback = "Komplettera", outcome = "needsWork" })).StatusCode);
    }

    [Fact]
    public async Task Grade_TeacherCanCorrectAGradedSubmission_AfterTheLastDeadline()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past.AddDays(-5), _past);
        await GradeAsync(submissionId, new { level = "fail", feedback = "Saknas." });

        var response = await GradeAsync(submissionId, new { level = "pass", feedback = "Rättat." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Pass", (await response.Content.ReadFromJsonAsync<SubmissionResponse>())!.Grade!.Level);
    }

    [Fact]
    public async Task Grade_OnSite_FollowsTheSameTimingRule()
    {
        var (groupId, _) = await AddGroupAsync(students: 2);
        var assignment = await _client.CreateAssignmentAsync(
            new { submissionModes = new[] { "onSite" }, gradeScale = "passFail" });
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        var rows = await ListAsync(linkId);
        await SetDeadlinesAsync(linkId, _past, _future);

        var early = await GradeAsync(rows[0].Id, new { level = "fail", feedback = "Saknas." });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        var work = await GradeAsync(rows[0].Id, new { feedback = "Komplettera", outcome = "needsWork" });
        Assert.Equal(HttpStatusCode.OK, work.StatusCode);

        await SetDeadlinesAsync(linkId, _past.AddDays(-5), _past);
        var late = await GradeAsync(rows[1].Id, new { level = "fail", feedback = "Saknas." });
        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        var lateWork = await GradeAsync(rows[0].Id, new { feedback = "Komplettera", outcome = "needsWork" });
        Assert.Equal(HttpStatusCode.BadRequest, lateWork.StatusCode);
    }

    // --- Personlig deadline för en elev ---

    private async Task<HttpResponseMessage> PutPersonalDueAtAsync(
        Guid linkId, Guid studentId, DateTimeOffset? dueAt, Guid? actingUser = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/links/{linkId}/students/{studentId}/personal-due-at")
        {
            Content = JsonContent.Create(new { dueAt })
        };
        if (actingUser is not null)
        {
            request.Headers.Add(TestAuthHandler.Header, actingUser.Value.ToString());
        }

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task PersonalDueAt_SetOnAnExistingRow_IsSavedAndNotifiesTheStudent()
    {
        var (linkId, submissionId) = await LinkWithStatusAsync(SubmissionStatus.NotStarted, _past, _past.AddDays(1));
        var studentId = await SubmitterAsync(linkId);
        var personal = DateTimeOffset.UtcNow.AddDays(3);

        var response = await PutPersonalDueAtAsync(linkId, studentId, personal);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var saved = await db.Submissions.AsNoTracking().SingleAsync(row => row.Id == submissionId);
        Assert.Equal(personal.UtcDateTime, saved.PersonalDueAt!.Value.UtcDateTime, TimeSpan.FromSeconds(1));
        var notification = await db.Notifications.SingleAsync(row => row.SubmissionId == submissionId);
        Assert.Equal(NotificationKind.PersonalDeadlineSet, notification.Kind);
        Assert.Equal(studentId, notification.UserId);
    }

    [Fact]
    public async Task PersonalDueAt_ForAStudentWithoutARow_CreatesANotStartedRow()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(_gradedFileSpec);
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        await SetDeadlinesAsync(linkId, _past);
        var lateStudent = await AddStudentToGroupAsync(groupId);

        var response = await PutPersonalDueAtAsync(linkId, lateStudent, DateTimeOffset.UtcNow.AddDays(2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = (await ListAsync(linkId)).Single();
        Assert.Equal("NotStarted", row.Status);
        Assert.Equal(lateStudent, row.StudentId);
    }

    [Fact]
    public async Task PersonalDueAt_ForAStudentOutsideTheAudience_IsNotFound()
    {
        var (groupId, _) = await AddGroupAsync(students: 0);
        var (otherGroupId, _) = await AddGroupAsync(students: 0);
        var assignment = await _client.CreateAssignmentAsync(_gradedFileSpec);
        var linkId = (await _client.CreateLinkAsync(assignment, new { groupId })).Id;
        await SetDeadlinesAsync(linkId, _past);
        var outsider = await AddStudentToGroupAsync(otherGroupId);

        var response = await PutPersonalDueAtAsync(linkId, outsider, DateTimeOffset.UtcNow.AddDays(2));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PersonalDueAt_ByAStudent_IsForbidden()
    {
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.NotStarted, _past);
        var studentId = await SubmitterAsync(linkId);

        var response = await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddDays(2), studentId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PersonalDueAt_InThePast_IsRefused()
    {
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.NotStarted, _past);
        var studentId = await SubmitterAsync(linkId);

        var response = await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddHours(-1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("framtiden", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PersonalDueAt_OnALinkWithoutDueDate_IsRefused()
    {
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.NotStarted, null);
        var studentId = await SubmitterAsync(linkId);

        var response = await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddDays(2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PersonalDueAt_OnAGradedSubmission_IsRefused()
    {
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.Graded, _past);
        var studentId = await SubmitterAsync(linkId);

        var response = await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddDays(2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PersonalDueAt_OnASubmissionWaitingForGrading_IsRefused()
    {
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past);
        var studentId = await SubmitterAsync(linkId);

        var response = await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddDays(2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("komplettering", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PersonalDueAt_BeforeTheLastClassDeadline_IsRefused()
    {
        // Klassen har en kompletteringsdeadline om tio dagar. En egen deadline före den gör ingen skillnad.
        var (linkId, _) = await LinkWithStatusAsync(SubmissionStatus.NeedsWork, _past, _future);
        var studentId = await SubmitterAsync(linkId);

        var early = await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddDays(2));
        var late = await PutPersonalDueAtAsync(linkId, studentId, _future.AddDays(2));

        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains("sista deadline", await early.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
    }

    [Fact]
    public async Task PersonalDueAt_Cleared_RemovesTheDateWithoutANewNotification()
    {
        var (linkId, submissionId) = await LinkWithStatusAsync(SubmissionStatus.NotStarted, _past);
        var studentId = await SubmitterAsync(linkId);
        await PutPersonalDueAtAsync(linkId, studentId, DateTimeOffset.UtcNow.AddDays(2));

        var response = await PutPersonalDueAtAsync(linkId, studentId, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Null((await db.Submissions.AsNoTracking().SingleAsync(row => row.Id == submissionId)).PersonalDueAt);
        Assert.Equal(1, await db.Notifications.CountAsync(row => row.SubmissionId == submissionId));
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public async Task Submit_AfterAllClassDeadlines_IsAllowedWhileAPersonalDeadlineRemains(SubmissionStatus status)
    {
        var (linkId, submissionId) = await LinkWithStatusAsync(status, _past.AddDays(-5), _past);
        await SetPersonalDueAtDirectAsync(submissionId, DateTimeOffset.UtcNow.AddDays(2));

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task Submit_AfterAnExpiredPersonalDeadline_IsRefused()
    {
        var (linkId, submissionId) = await LinkWithStatusAsync(SubmissionStatus.NotStarted, _past.AddDays(-5), _past);
        await SetPersonalDueAtDirectAsync(submissionId, DateTimeOffset.UtcNow.AddDays(-1));

        var result = await SubmitFilesAsync(linkId, "rapport.pdf");

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Grade_NeedsWork_IsAllowedWhileAPersonalDeadlineRemains()
    {
        var (_, submissionId) = await LinkWithStatusAsync(SubmissionStatus.Submitted, _past.AddDays(-5), _past);
        await SetPersonalDueAtDirectAsync(submissionId, DateTimeOffset.UtcNow.AddDays(2));

        var fail = await GradeAsync(submissionId, new { level = "fail", feedback = "Saknas." });
        var work = await GradeAsync(submissionId, new { feedback = "Komplettera", outcome = "needsWork" });

        Assert.Equal(HttpStatusCode.BadRequest, fail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, work.StatusCode);
    }

    private async Task SetPersonalDueAtDirectAsync(Guid submissionId, DateTimeOffset dueAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        (await db.Submissions.FindAsync(submissionId))!.PersonalDueAt = dueAt;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddStudentToGroupAsync(Guid groupId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await db.Groups.FindAsync(groupId);
        var user = await TestData.AddMemberAsync(db, group!);
        return user.Id;
    }

    [Fact]
    public async Task Submit_GroupRepoAssignment_MarksEveryGroupMemberAsSubmitted()
    {
        var (groupId, schoolId) = await AddGroupAsync(students: 3);
        Guid courseId;
        Guid firstStudentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            courseId = (await TestData.AddCourseAsync(db, schoolId, groupId)).Id;
            firstStudentId = (await db.Enrollments
                .Where(enrollment => enrollment.GroupId == groupId && enrollment.Role == GroupRole.Student)
                .OrderBy(enrollment => enrollment.UserId)
                .FirstAsync()).UserId;
        }

        var created = await _client.PostAsJsonAsync("/api/content/with-link", new
        {
            content = new
            {
                type = "assignment",
                title = "Grupplabb",
                bodyMd = "",
                status = "published",
                gradeScale = "passFail",
                submissionModes = new[] { "repo" },
                templateRepo = "org/template",
                workMode = "group",
                repoNamePattern = "labb-{groupSlug}"
            },
            link = new
            {
                courseId,
                groups = new[]
                {
                    new
                    {
                        name = "Grupp ett",
                        studentIds = (await StudentsAsync(groupId)).ToArray()
                    }
                }
            }
        });
        created.EnsureSuccessStatusCode();
        var linkId = (await created.Content.ReadFromJsonAsync<CreatedWithLink>())!.Link.Id;

        using (var submitScope = factory.Services.CreateScope())
        {
            TestData.ActAs(submitScope.ServiceProvider, firstStudentId);
            var service = submitScope.ServiceProvider.GetRequiredService<SubmissionService>();
            var result = await service.SubmitAsync(linkId, new SubmitDto(), [], CancellationToken.None);
            Assert.Equal(ResultStatus.Ok, result.Status);
        }

        var rows = await ListAsync(linkId);
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal("Submitted", row.Status));
        Assert.All(rows, row => Assert.NotNull(row.Repo));
    }

    private async Task<List<Guid>> StudentsAsync(Guid groupId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Enrollments
            .Where(enrollment => enrollment.GroupId == groupId && enrollment.Role == GroupRole.Student)
            .Select(enrollment => enrollment.UserId)
            .ToListAsync();
    }

    // Inlämningen skickas som multipart/form-data, precis som från formuläret, även när den bara har text.
    private async Task<HttpResponseMessage> PostSubmissionAsync(Guid linkId, string bodyMd)
    {
        var form = new MultipartFormDataContent { { new StringContent(bodyMd), "bodyMd" } };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/links/{linkId}/submissions") { Content = form };
        request.Headers.Add(TestAuthHandler.Header, (await SubmitterAsync(linkId)).ToString());
        return await _client.SendAsync(request);
    }

    private async Task<Guid> SubmitterAsync(Guid linkId)
    {
        using var scope = factory.Services.CreateScope();
        return await TestData.SubmitterAsync(scope.ServiceProvider.GetRequiredService<AppDb>(), linkId);
    }

    private async Task<List<SubmissionResponse>> ListAsync(Guid linkId) =>
        await _client.GetJsonAsync<List<SubmissionResponse>>($"/api/links/{linkId}/submissions");

    private async Task<SubmissionResponse> SubmitAsync(Guid linkId)
    {
        var result = await SubmitFilesAsync(linkId, "rapport.pdf");
        Assert.Equal(ResultStatus.Ok, result.Status);
        return await _client.GetJsonAsync<SubmissionResponse>($"/api/submissions/{result.Value!.Id}");
    }

    // Lämnas in genom servicen, så att testet inte beror på hur formuläret kodas (det täcks i FileUploadTests).
    private Task<ServiceResult<SubmissionDto>> SubmitFilesAsync(Guid linkId, params string[] fileNames) =>
        SubmitFilesAsync(linkId, null, fileNames);

    private async Task<ServiceResult<SubmissionDto>> SubmitFilesAsync(
        Guid linkId, List<Guid>? keepFileIds, params string[] fileNames)
    {
        var submitter = await SubmitterAsync(linkId);
        using var scope = factory.Services.CreateScope();
        TestData.ActAs(scope.ServiceProvider, submitter);
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
        var group = await TestData.AddGroupAsync(db, placeholderTeaches: true);
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
