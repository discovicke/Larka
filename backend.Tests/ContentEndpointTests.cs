using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Backend.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class ContentEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private InMemoryFileStorage Storage =>
        (InMemoryFileStorage)factory.Services.GetRequiredService<IFileStorage>();

    private record ContentResponse(
        Guid Id, string Type, string Title, Guid AuthorId, string BodyMd, string Status);

    private record ContentDetail(
        Guid Id, string Type, string Title, string BodyMd, string Status, AssignmentDetail? Assignment);

    private record AssignmentDetail(
        string GradeScale,
        List<string> SubmissionModes,
        string? TemplateRepo,
        string? RepoNamePattern = null,
        string? WorkMode = null,
        bool LockAtDeadline = true,
        bool FeedbackPullRequest = false,
        bool AllowLate = false,
        List<RequirementDetail>? Requirements = null);

    private record RequirementDetail(
        string Title,
        string? TestPattern,
        bool IsManual,
        int Position);

    private record RepoNamePreview(string Name, string Pattern);

    private record FeedItem(
        Guid ContentId, string Type, string Title, string AuthorName, Guid? GroupId, Guid? CourseId, bool IsPinned);

    private record LinkResponse(Guid Id, Guid ContentId, Guid? CourseId, Guid? ModuleId);

    private record CreatedWithLink(ContentResponse Content, LinkResponse Link);

    private record MoveResult(Guid LinkId, Guid ModuleId, int Position, List<Guid> AffectedLinkIds);

    private record ModuleResponse(Guid Id);

    private record ModuleWithContent(Guid Id, List<ModuleItem> Content);

    private record ModuleItem(
        Guid LinkId,
        Guid ContentId,
        string Title,
        string Status,
        DateTimeOffset PublishAt,
        DateTimeOffset? DueAt,
        bool IsPinned,
        List<Guid> BlockedBy);

    [Fact]
    public async Task CreateContent_ReturnsCreated_WithPlaceholderAuthor()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/content", new { type = "news", title = "Välkommen", bodyMd = "Hej!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ContentResponse>();
        Assert.Equal("News", content!.Type);
        Assert.Equal(PlaceholderIds.UserId, content.AuthorId);
    }

    [Fact]
    public async Task CreateContent_WithUnknownType_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content", new { type = "meme", title = "x", bodyMd = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateContentWithLink_CreatesContentAndLinkInOneRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");

        var response = await _client.PostAsJsonAsync("/api/content/with-link", new
        {
            content = new { type = "theory", title = "Ny del", bodyMd = "Text" },
            link = new { courseId, moduleId = week.Id }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedWithLink>();
        Assert.Equal("Ny del", created!.Content.Title);
        Assert.Equal(week.Id, created.Link.ModuleId);
        var listed = (await GetModulesAsync(courseId)).Single().Content.Single();
        Assert.Equal(("Ny del", created.Content.Id, created.Link.Id), (listed.Title, listed.ContentId, listed.LinkId));
    }

    [Fact]
    public async Task CreateContentWithLink_WithInvalidLink_DoesNotLeaveOrphanContent()
    {
        var courseId = await AddCourseAsync();
        var before = await CountContentAsync();

        var response = await _client.PostAsJsonAsync("/api/content/with-link", new
        {
            content = new { type = "theory", title = "Ska inte sparas", bodyMd = "" },
            link = new { courseId, moduleId = Guid.NewGuid() }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountContentAsync());
    }

    [Fact]
    public async Task LinkToGroup_ShowsInGroupFeed()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("theory", "Loopar");

        var link = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links", new { groupId });

        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        var feed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");
        Assert.Contains(feed!, item =>
            item.ContentId == content.Id && item.Title == "Loopar" && item.AuthorName == "Placeholder");
    }

    [Fact]
    public async Task LinkToCourse_ShowsInFeedOfGroupsThatReadIt_Only()
    {
        var (readingGroupId, courseId) = await AddGroupWithCourseAsync();
        var (otherGroupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("theory", "Kursmaterial");

        await _client.PostAsJsonAsync($"/api/content/{content.Id}/links", new { courseId });

        var readingFeed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{readingGroupId}/content");
        var otherFeed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{otherGroupId}/content");
        var courseFeed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/courses/{courseId}/content");
        Assert.Contains(readingFeed!, item => item.ContentId == content.Id);
        Assert.DoesNotContain(otherFeed!, item => item.ContentId == content.Id);
        Assert.Contains(courseFeed!, item => item.ContentId == content.Id);
    }

    [Fact]
    public async Task GroupFeed_FiltersOnType()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var news = await CreateAsync("news", "Nyhet");
        var theory = await CreateAsync("theory", "Teori");
        await _client.PostAsJsonAsync($"/api/content/{news.Id}/links", new { groupId });
        await _client.PostAsJsonAsync($"/api/content/{theory.Id}/links", new { groupId });

        var feed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content?type=news");

        Assert.Equal([news.Id], feed!.Select(item => item.ContentId));
    }

    [Fact]
    public async Task Draft_IsNotInFeed()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var response = await _client.PostAsJsonAsync("/api/content",
            new { type = "news", title = "Utkast", bodyMd = "", status = "draft" });
        var draft = await response.Content.ReadFromJsonAsync<ContentResponse>();
        await _client.PostAsJsonAsync($"/api/content/{draft!.Id}/links", new { groupId });

        var feed = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");

        Assert.DoesNotContain(feed!, item => item.ContentId == draft.Id);
    }

    [Fact]
    public async Task FutureWeek_KeepsItsContentOutOfTheFeedUntilItOpens()
    {
        var (groupId, courseId) = await AddGroupWithCourseAsync();
        var publishAt = new DateTimeOffset(2099, 1, 5, 8, 0, 0, TimeSpan.Zero);
        var weekResponse = await _client.PostAsJsonAsync(
            $"/api/courses/{courseId}/modules", new { title = "Framtiden", publishAt });
        var week = (await weekResponse.Content.ReadFromJsonAsync<ModuleResponse>())!;
        var news = await CreateAsync("news", "Nyhet i framtiden");
        await _client.PostAsJsonAsync(
            $"/api/content/{news.Id}/links", new { courseId, moduleId = week.Id });

        var before = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");
        Assert.DoesNotContain(before!, item => item.ContentId == news.Id);

        await _client.PutAsJsonAsync(
            $"/api/courses/{courseId}/modules/{week.Id}",
            new { title = "Framtiden", description = "", publishAt = (DateTimeOffset?)null });
        var after = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");

        Assert.Contains(after!, item => item.ContentId == news.Id);
    }

    [Fact]
    public async Task Link_WithoutGroupOrCourse_ReturnsBadRequest()
    {
        var content = await CreateAsync("news", "Ingenstans");

        var response = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links", new { isPinned = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_ToGroupThatDoesNotExist_ReturnsBadRequest()
    {
        var content = await CreateAsync("news", "Fel grupp");

        var response = await _client.PostAsJsonAsync(
            $"/api/content/{content.Id}/links", new { groupId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_ToGroupAndCourseItDoesNotRead_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var (_, otherCourseId) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("theory", "Fel kurs");

        var response = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links",
            new { groupId, courseId = otherCourseId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_WithDueAtOnNews_ReturnsBadRequest()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateAsync("news", "Nyhet med deadline");

        var response = await _client.PostAsJsonAsync($"/api/content/{content.Id}/links",
            new { groupId, dueAt = DateTimeOffset.UtcNow.AddDays(7) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SameAssignment_CanHaveDifferentDeadlinesPerGroup()
    {
        var (groupA, _) = await AddGroupWithCourseAsync();
        var (groupB, _) = await AddGroupWithCourseAsync();
        var assignment = await CreateAsync("assignment", "Labb 1");

        var linkA = await _client.PostAsJsonAsync($"/api/content/{assignment.Id}/links",
            new { groupId = groupA, dueAt = DateTimeOffset.UtcNow.AddDays(7) });
        var linkB = await _client.PostAsJsonAsync($"/api/content/{assignment.Id}/links",
            new { groupId = groupB, dueAt = DateTimeOffset.UtcNow.AddDays(14) });

        Assert.Equal(HttpStatusCode.Created, linkA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, linkB.StatusCode);
    }

    [Fact]
    public async Task PublishAndUnpublish_ChangesTheFeed()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateTheoryAsync("Publicera", "draft");
        await LinkToGroupAsync(content.Id, groupId);

        var before = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");
        Assert.DoesNotContain(before!, item => item.ContentId == content.Id);

        var published = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/status", new { status = "published" });
        var afterPublish = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Contains(afterPublish!, item => item.ContentId == content.Id);

        await _client.PutAsJsonAsync($"/api/content/{content.Id}/status", new { status = "draft" });
        var afterUnpublish = await _client.GetFromJsonAsync<List<FeedItem>>($"/api/groups/{groupId}/content");

        Assert.DoesNotContain(afterUnpublish!, item => item.ContentId == content.Id);
    }

    [Fact]
    public async Task UpdateContent_ChangesFields_ButNotTypeOrStatus()
    {
        var content = await CreateTheoryAsync("Gammal", "draft");

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}", new { title = " Ny ", bodyMd = "**text**" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ContentResponse>();
        Assert.Equal(
            ("Theory", "Ny", "**text**", "Draft"),
            (updated!.Type, updated.Title, updated.BodyMd, updated.Status));

        var invalid = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}", new { title = "Teori", bodyMd = "", submissionModes = new[] { "file" } });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task UpdateAssignment_ChangesGradeScaleAndModes()
    {
        var content = await CreateAssignmentAsync("Labb", gradeScale: "passFail", submissionModes: ["file"]);

        var response = await _client.PutAsJsonAsync($"/api/content/{content.Id}", new
        {
            title = "Labb 2",
            bodyMd = "",
            gradeScale = "passFailDistinction",
            submissionModes = new[] { "text" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ContentDetail>();
        Assert.Equal("PassFailDistinction", updated!.Assignment!.GradeScale);
        Assert.Equal(["Text"], updated.Assignment.SubmissionModes);

        var invalid = await _client.PutAsJsonAsync($"/api/content/{content.Id}", new
        {
            title = "Labb 2",
            bodyMd = "",
            submissionModes = new[] { "repo" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task CreateRepoAssignment_StoresGitHubSettings()
    {
        var content = await CreateAssignmentAsync(
            "Labb 2",
            gradeScale: "passFail",
            submissionModes: ["repo"],
            templateRepo: "org/template",
            repoNamePattern: "lab2-{githubLogin}");

        var detail = await _client.GetFromJsonAsync<ContentDetail>($"/api/content/{content.Id}");

        Assert.Equal("lab2-{githubLogin}", detail!.Assignment!.RepoNamePattern);
        Assert.Equal("Individual", detail.Assignment.WorkMode);
        Assert.True(detail.Assignment.LockAtDeadline);
        Assert.False(detail.Assignment.FeedbackPullRequest);
        Assert.False(detail.Assignment.AllowLate);
    }

    [Fact]
    public async Task RepoAssignment_StoresRequirementsInOrder()
    {
        var content = await CreateAssignmentAsync(
            "Labb 2",
            gradeScale: "passFail",
            submissionModes: ["repo"],
            templateRepo: "org/template",
            requirements:
            [
                new { title = "Bygger", testPattern = "Build.*", isManual = false },
                new { title = "Reflektion", testPattern = (string?)null, isManual = true }
            ]);

        var detail = await _client.GetFromJsonAsync<ContentDetail>($"/api/content/{content.Id}");

        Assert.Equal(2, detail!.Assignment!.Requirements!.Count);
        Assert.Equal("Bygger", detail.Assignment.Requirements[0].Title);
        Assert.Equal("Build.*", detail.Assignment.Requirements[0].TestPattern);
        Assert.Equal(0, detail.Assignment.Requirements[0].Position);
        Assert.True(detail.Assignment.Requirements[1].IsManual);
    }

    [Fact]
    public async Task CreateRepoAssignment_WithInvalidPattern_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title = "Labb",
            bodyMd = "",
            gradeScale = "passFail",
            submissionModes = new[] { "repo" },
            templateRepo = "org/template",
            repoNamePattern = "lab2/{githubLogin}"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRepoAssignment_WithGroupMode_RequiresGroups()
    {
        var (courseId, _, _) = await AddCourseWithTwoStudentsAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");

        var response = await _client.PostAsJsonAsync("/api/content/with-link", new
        {
            content = new
            {
                type = "assignment",
                title = "Labb",
                bodyMd = "",
                gradeScale = "passFail",
                submissionModes = new[] { "repo" },
                templateRepo = "org/template",
                workMode = "group",
                repoNamePattern = "labb-{groupSlug}"
            },
            link = new { courseId, moduleId = week.Id }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateGroupRepoAssignment_CreatesGroupsSubmissionsAndSharedRepo()
    {
        var (courseId, firstId, secondId) = await AddCourseWithTwoStudentsAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");

        var response = await _client.PostAsJsonAsync("/api/content/with-link", new
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
                moduleId = week.Id,
                groups = new[]
                {
                    new { name = "Grupp ett", studentIds = new[] { firstId } },
                    new { name = "Grupp tva", studentIds = new[] { secondId } }
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreatedWithLink>();
        var linkId = body!.Link.Id;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var groups = await db.AssignmentGroups
            .Where(group => group.ContentLinkId == linkId)
            .OrderBy(group => group.Position)
            .ToListAsync();
        Assert.Equal(2, groups.Count);
        Assert.Equal(["Grupp ett", "Grupp tva"], groups.Select(group => group.Name).ToArray());

        var submissions = await db.Submissions
            .Where(submission => submission.ContentLinkId == linkId)
            .OrderBy(submission => submission.StudentId)
            .ToListAsync();
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, submission => Assert.NotNull(submission.AssignmentGroupId));

        var submissionIds = submissions.Select(submission => submission.Id).ToList();
        var repos = await db.StudentRepos
            .Where(repo => submissionIds.Contains(repo.SubmissionId))
            .OrderBy(repo => repo.AssignmentGroupId)
            .ToListAsync();
        Assert.Equal(2, repos.Count);
        Assert.All(repos, repo => Assert.NotNull(repo.AssignmentGroupId));
        Assert.Equal(
            ["labb-grupp-ett", "labb-grupp-tva"],
            repos.Select(repo => repo.DesiredRepoName!).Order().ToArray());
    }

    [Fact]
    public async Task LockGroupRepos_MarksEveryGroupMemberAsSubmitted()
    {
        var (courseId, firstId, secondId) = await AddCourseWithTwoStudentsAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
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
                moduleId = week.Id,
                groups = new[]
                {
                    new { name = "Grupp ett", studentIds = new[] { firstId } },
                    new { name = "Grupp tva", studentIds = new[] { secondId } }
                }
            }
        });
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<CreatedWithLink>();
        var linkId = body!.Link.Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submissions = await db.Submissions
            .Where(submission => submission.ContentLinkId == linkId)
            .ToListAsync();
        var submissionIds = submissions.Select(submission => submission.Id).ToList();
        var repos = await db.StudentRepos
            .Where(repo => submissionIds.Contains(repo.SubmissionId))
            .ToListAsync();
        Assert.Equal(2, repos.Count);
        Assert.All(repos, repo => Assert.NotNull(repo.AssignmentGroupId));
        foreach (var repo in repos)
        {
            repo.LastSha = "abc123";
            repo.ProvisioningState = RepoProvisioningState.Ready;
        }
        await db.SaveChangesAsync();

        using (var verifyScope = factory.Services.CreateScope())
        {
            var repository = verifyScope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
            var contexts = await repository.GetRepoContextsForLinkAsync(
                linkId, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(2, contexts.Count);
            Assert.All(contexts, context =>
                Assert.Equal(RepoProvisioningState.Ready, context.Repo.ProvisioningState));
            Assert.All(contexts, context => Assert.Equal("abc123", context.Repo.LastSha));
        }

        var response = await _client.PostAsync($"/api/links/{linkId}/repos/lock", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var afterScope = factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<AppDb>();
        submissions = await afterDb.Submissions
            .Where(submission => submission.ContentLinkId == linkId)
            .ToListAsync();
        repos = await afterDb.StudentRepos
            .Where(repo => submissionIds.Contains(repo.SubmissionId))
            .ToListAsync();
        Assert.Equal(2, submissions.Count);
        Assert.All(repos, repo => Assert.Equal("abc123", repo.SubmissionSha));
        Assert.All(submissions, submission => Assert.Equal(SubmissionStatus.Submitted, submission.Status));
    }

    [Fact]
    public async Task PreviewRepoName_UsesSameNamingAsProvisioning()
    {
        var response = await _client.PostAsJsonAsync("/api/content/github/repo-name-preview", new
        {
            pattern = "lab2-{githubLogin}",
            assignmentTitle = "Labb 2",
            githubLogin = "alice"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<RepoNamePreview>();
        Assert.Equal("lab2-alice", preview!.Name);
        Assert.Equal("lab2-{githubLogin}", preview.Pattern);
    }

    [Fact]
    public async Task PreviewRepoName_WithInvalidPattern_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/content/github/repo-name-preview", new
        {
            pattern = "lab2/{githubLogin}",
            assignmentTitle = "Labb 2"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContentLink_ChangesPublishAtDeadlineAndPin()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateAssignmentAsync("Labb", submissionModes: ["file"]);
        var link = await LinkToModuleAsync(content.Id, courseId, week.Id);
        var publishAt = new DateTimeOffset(2027, 1, 2, 8, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2027, 1, 9, 17, 0, 0, TimeSpan.Zero);

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/links/{link.Id}",
            new { publishAt, dueAt, isPinned = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await GetModulesAsync(courseId)).Single().Content.Single();
        Assert.Equal((publishAt, dueAt, true), (item.PublishAt, item.DueAt, item.IsPinned));
    }

    [Fact]
    public async Task UpdateTheoryLink_WithDueAt_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateTheoryAsync("Teori");
        var link = await LinkToModuleAsync(content.Id, courseId, week.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/links/{link.Id}",
            new { dueAt = DateTimeOffset.UtcNow.AddDays(1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLink_ForAnotherContent_ReturnsNotFound()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var first = await CreateTheoryAsync("Första");
        var second = await CreateTheoryAsync("Andra");
        var link = await LinkToModuleAsync(first.Id, courseId, week.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{second.Id}/links/{link.Id}", new { publishAt = DateTimeOffset.UtcNow });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MoveLink_KeepsLinkAndDeadline_AndRemovesDependencies()
    {
        var courseId = await AddCourseAsync();
        var source = await CreateModuleAsync(courseId, "Vecka 1");
        var target = await CreateModuleAsync(courseId, "Vecka 2");
        var first = await CreateTheoryAsync("Första");
        var moved = await CreateAssignmentAsync("Flyttad", submissionModes: ["text"]);
        var last = await CreateTheoryAsync("Sista");
        var targetContent = await CreateTheoryAsync("Befintlig");
        var firstLink = await LinkToModuleAsync(first.Id, courseId, source.Id);
        var dueAt = new DateTimeOffset(2027, 3, 1, 17, 0, 0, TimeSpan.Zero);
        var movedResponse = await _client.PostAsJsonAsync(
            $"/api/content/{moved.Id}/links", new { courseId, moduleId = source.Id, dueAt });
        var movedLink = (await movedResponse.Content.ReadFromJsonAsync<LinkResponse>())!;
        var lastLink = await LinkToModuleAsync(last.Id, courseId, source.Id);
        await LinkToModuleAsync(targetContent.Id, courseId, target.Id);
        await SaveRoadmapAsync(courseId, source.Id,
            (firstLink.Id, []),
            (movedLink.Id, [firstLink.Id]),
            (lastLink.Id, [movedLink.Id]));

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{moved.Id}/links/{movedLink.Id}/module", new { moduleId = target.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<MoveResult>())!;
        Assert.Equal((movedLink.Id, target.Id), (result.LinkId, result.ModuleId));
        Assert.Contains(firstLink.Id, result.AffectedLinkIds);
        Assert.Contains(lastLink.Id, result.AffectedLinkIds);

        var modules = await GetModulesAsync(courseId);
        var sourceContent = modules.Single(module => module.Id == source.Id).Content;
        var targetWeek = modules.Single(module => module.Id == target.Id).Content;
        Assert.Equal(["Första", "Sista"], sourceContent.Select(item => item.Title));
        Assert.Equal(["Befintlig", "Flyttad"], targetWeek.Select(item => item.Title));
        var movedItem = targetWeek.Single(item => item.ContentId == moved.Id);
        Assert.Equal((movedLink.Id, dueAt), (movedItem.LinkId, movedItem.DueAt));
        Assert.All(sourceContent.Concat(targetWeek), item => Assert.Empty(item.BlockedBy));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(0, await db.ContentLinkDependencies.CountAsync());
    }

    [Fact]
    public async Task MoveLink_WithStudentActivity_ReturnsConflict()
    {
        var courseId = await AddCourseAsync();
        var source = await CreateModuleAsync(courseId, "Vecka 1");
        var target = await CreateModuleAsync(courseId, "Vecka 2");
        var content = await CreateTheoryAsync("Teori");
        var link = await LinkToModuleAsync(content.Id, courseId, source.Id);
        var (_, studentId) = await AddGroupWithStudentAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.PartCompletions.Add(new PartCompletion
            {
                ContentLinkId = link.Id,
                StudentId = studentId
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/links/{link.Id}/module", new { moduleId = target.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var modules = await GetModulesAsync(courseId);
        Assert.Equal(["Teori"], modules.Single(module => module.Id == source.Id).Content.Select(item => item.Title));
        Assert.Empty(modules.Single(module => module.Id == target.Id).Content);
    }

    [Fact]
    public async Task MoveLink_ToModuleOfAnotherCourse_ReturnsBadRequest()
    {
        var courseId = await AddCourseAsync();
        var source = await CreateModuleAsync(courseId, "Vecka 1");
        var otherWeek = await CreateModuleAsync(await AddCourseAsync(), "Annan kurs");
        var content = await CreateTheoryAsync("Teori");
        var link = await LinkToModuleAsync(content.Id, courseId, source.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/content/{content.Id}/links/{link.Id}/module", new { moduleId = otherWeek.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_WhenLastLink_DeletesContentAndCompactsPositions()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var first = await CreateTheoryAsync("Första");
        var second = await CreateTheoryAsync("Andra");
        var third = await CreateTheoryAsync("Tredje");
        await LinkToModuleAsync(first.Id, courseId, week.Id);
        var middleLink = await LinkToModuleAsync(second.Id, courseId, week.Id);
        await LinkToModuleAsync(third.Id, courseId, week.Id);

        var response = await _client.DeleteAsync($"/api/content/{second.Id}/links/{middleLink.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/content/{second.Id}")).StatusCode);
        var content = (await GetModulesAsync(courseId)).Single().Content;
        Assert.Equal(["Första", "Tredje"], content.Select(item => item.Title));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var positions = await db.ContentLinks
            .Where(link => link.ModuleId == week.Id)
            .OrderBy(link => link.Position)
            .Select(link => link.Position)
            .ToListAsync();
        Assert.Equal([1, 2], positions);
    }

    [Fact]
    public async Task DeleteLink_WhenContentHasAnotherLink_KeepsContent()
    {
        var courseId = await AddCourseAsync();
        var firstWeek = await CreateModuleAsync(courseId, "Vecka 1");
        var secondWeek = await CreateModuleAsync(courseId, "Vecka 2");
        var content = await CreateTheoryAsync("Delad");
        var firstLink = await LinkToModuleAsync(content.Id, courseId, firstWeek.Id);
        await LinkToModuleAsync(content.Id, courseId, secondWeek.Id);

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{firstLink.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/content/{content.Id}")).StatusCode);
        var modules = await GetModulesAsync(courseId);
        Assert.Empty(modules.Single(module => module.Id == firstWeek.Id).Content);
        Assert.Single(modules.Single(module => module.Id == secondWeek.Id).Content);
    }

    [Fact]
    public async Task DeleteLink_WithStudentActivity_ReturnsConflict()
    {
        var (groupId, _) = await AddGroupWithStudentAsync();
        var content = await CreateAssignmentAsync("Övning", submissionModes: ["file"]);
        var link = await LinkToGroupAsync(content.Id, groupId);
        await SetSubmissionStatusAsync(link.Id, SubmissionStatus.Submitted);

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_RemovesNotStartedRowsFromHandout()
    {
        var (groupId, _) = await AddGroupWithStudentAsync();
        var content = await CreateAssignmentAsync("Övning", submissionModes: ["file"]);
        var link = await LinkToGroupAsync(content.Id, groupId);
        Assert.Equal(1, await CountSubmissionsAsync(link.Id));

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountSubmissionsAsync(link.Id));
    }

    [Fact]
    public async Task DeleteLink_WithRepoAssignmentWithoutStudentWork_IsAllowed()
    {
        var (groupId, _) = await AddGroupWithStudentAsync();
        var content = await CreateAssignmentAsync(
            "Labb", submissionModes: ["repo"], templateRepo: "org/template");
        var link = await LinkToGroupAsync(content.Id, groupId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var submissionId = await db.Submissions
                .Where(submission => submission.ContentLinkId == link.Id)
                .Select(submission => submission.Id)
                .SingleAsync();
            Assert.True(await db.StudentRepos.AnyAsync(repo => repo.SubmissionId == submissionId));
        }

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountSubmissionsAsync(link.Id));
    }

    [Fact]
    public async Task DeleteLink_WithStudentRepoCommit_ReturnsConflict()
    {
        var (groupId, _) = await AddGroupWithStudentAsync();
        var content = await CreateAssignmentAsync(
            "Labb", submissionModes: ["repo"], templateRepo: "org/template");
        var link = await LinkToGroupAsync(content.Id, groupId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var submissionId = await db.Submissions
                .Where(submission => submission.ContentLinkId == link.Id)
                .Select(submission => submission.Id)
                .SingleAsync();
            var repo = await db.StudentRepos.SingleAsync(candidate => candidate.SubmissionId == submissionId);
            repo.InitialSha = "initial";
            repo.LastSha = "student";
            await db.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeletePublishedGradedAssignment_ReturnsConflict_UntilUnpublished()
    {
        var courseId = await AddCourseAsync();
        var week = await CreateModuleAsync(courseId, "Vecka 1");
        var content = await CreateAssignmentAsync("Inlämning", gradeScale: "passFail");
        var link = await LinkToModuleAsync(content.Id, courseId, week.Id);

        var blocked = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");
        await _client.PutAsJsonAsync($"/api/content/{content.Id}/status", new { status = "draft" });
        var deleted = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_WithWrongLink_ReturnsNotFound()
    {
        var content = await CreateTheoryAsync("Teori");

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_LastLink_DeletesAttachmentsFromStorage()
    {
        var (groupId, _) = await AddGroupWithCourseAsync();
        var content = await CreateTheoryAsync("Material med bilaga");
        await UploadAttachmentAsync(content.Id);
        var key = Assert.Single(Storage.Keys, storageKey => storageKey.StartsWith($"content/{content.Id}/"));
        var link = await LinkToGroupAsync(content.Id, groupId);

        var response = await _client.DeleteAsync($"/api/content/{content.Id}/links/{link.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(key, Storage.Keys);
    }

    [Fact]
    public async Task Comment_OnContent_IsListed()
    {
        var content = await CreateAsync("news", "Kommentera mig");

        var created = await _client.PostAsJsonAsync($"/api/content/{content.Id}/comments", new { bodyMd = "Bra!" });
        var comments = await _client.GetStringAsync($"/api/content/{content.Id}/comments");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("Bra!", comments);
        Assert.Contains("\"authorName\":\"Placeholder\"", comments);
        Assert.Contains("\"authorName\":\"Placeholder\"", await created.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Comment_OnContentThatDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/content/{Guid.NewGuid()}/comments", new { bodyMd = "Hallå?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Comment_OnDraft_ReturnsNotFoundForAStudent_AndAddsNothing()
    {
        var draft = await CreateTheoryAsync("Utkast", "draft");
        using var student = await StudentClientAsync();

        var response = await student.PostAsJsonAsync($"/api/content/{draft.Id}/comments", new { bodyMd = "Smyg" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Smyg", await _client.GetStringAsync($"/api/content/{draft.Id}/comments"));
    }

    [Fact]
    public async Task Comment_OnPublishedContent_IsAllowedForAStudent()
    {
        var published = await CreateTheoryAsync("Publicerad");
        using var student = await StudentClientAsync();

        var response = await student.PostAsJsonAsync($"/api/content/{published.Id}/comments", new { bodyMd = "Tack" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpClient> StudentClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var student = await TestData.AddMemberAsync(db, group);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, student.Id.ToString());
        return client;
    }

    private async Task<ContentResponse> CreateAsync(string type, string title)
    {
        var response = await _client.PostAsJsonAsync("/api/content", new { type, title, bodyMd = "" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }

    private async Task<ContentResponse> CreateTheoryAsync(string title, string status = "published")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/content", new { type = "theory", title, bodyMd = "", status });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }

    private async Task<ContentResponse> CreateAssignmentAsync(
        string title,
        string status = "published",
        string? gradeScale = null,
        string[]? submissionModes = null,
        string? templateRepo = null,
        string? repoNamePattern = null,
        object[]? requirements = null)
    {
        var response = await _client.PostAsJsonAsync("/api/content", new
        {
            type = "assignment",
            title,
            bodyMd = "",
            status,
            gradeScale,
            submissionModes,
            templateRepo,
            repoNamePattern,
            requirements
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContentResponse>())!;
    }

    private async Task<ModuleResponse> CreateModuleAsync(Guid courseId, string title)
    {
        var response = await _client.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ModuleResponse>())!;
    }

    private async Task<LinkResponse> LinkToModuleAsync(Guid contentId, Guid courseId, Guid moduleId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/content/{contentId}/links", new { courseId, moduleId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LinkResponse>())!;
    }

    private async Task<LinkResponse> LinkToGroupAsync(Guid contentId, Guid groupId)
    {
        var response = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", new { groupId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LinkResponse>())!;
    }

    private async Task<List<ModuleWithContent>> GetModulesAsync(Guid courseId) =>
        (await _client.GetFromJsonAsync<List<ModuleWithContent>>($"/api/courses/{courseId}/modules"))!;

    private async Task<Guid> AddCourseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId)).Id;
    }

    private async Task<(Guid CourseId, Guid FirstStudentId, Guid SecondStudentId)>
        AddCourseWithTwoStudentsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id);
        var first = await TestData.AddMemberAsync(db, group);
        var second = await TestData.AddMemberAsync(db, group);
        return (course.Id, first.Id, second.Id);
    }

    private async Task<(Guid GroupId, Guid StudentId)> AddGroupWithStudentAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db, placeholderTeaches: true);
        var student = await TestData.AddMemberAsync(db, group);
        return (group.Id, student.Id);
    }

    private async Task SetSubmissionStatusAsync(Guid linkId, SubmissionStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var submission = await db.Submissions.SingleAsync(candidate => candidate.ContentLinkId == linkId);
        submission.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<int> CountSubmissionsAsync(Guid linkId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Submissions.CountAsync(submission => submission.ContentLinkId == linkId);
    }

    private async Task<int> CountContentAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return await db.Content.CountAsync();
    }

    private async Task SaveRoadmapAsync(
        Guid courseId, Guid moduleId, params (Guid LinkId, Guid[] BlockedBy)[] links)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/courses/{courseId}/modules/{moduleId}/roadmap",
            new
            {
                links = links.Select(link => new
                {
                    linkId = link.LinkId,
                    blockedByLinkIds = link.BlockedBy,
                    completionRule = (string?)null
                })
            });
        response.EnsureSuccessStatusCode();
    }

    private async Task UploadAttachmentAsync(Guid contentId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "slides.pdf");
        var response = await _client.PostAsync($"/api/content/{contentId}/attachments", form);
        response.EnsureSuccessStatusCode();
    }

    private record CreatedResponse(Guid Id);

    // Skola, grupp och kurs skapas via API:et, och gruppen får läsa kursen.
    private async Task<(Guid GroupId, Guid CourseId)> AddGroupWithCourseAsync()
    {
        // Adminen i testerna ser bara sin egen skola, så allt skapas i den.
        var schoolId = PlaceholderIds.SchoolId;
        var unique = Guid.NewGuid().ToString("N")[..8];
        var group = await PostAsync("/api/groups", new { schoolId, type = "class", name = $"NET-{unique}" });
        var course = await PostAsync("/api/courses", new { schoolId, title = "Kurs", code = $"KURS-{unique}" });
        var link = await _client.PostAsync($"/api/groups/{group.Id}/courses/{course.Id}", null);
        link.EnsureSuccessStatusCode();

        // Admin undervisar inte av sig själv: placeholder blir lärare i kursen och i gruppen.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await TestData.MakePlaceholderTeacherAsync(db, course.Id);
        db.Enrollments.Add(new Enrollment
        {
            UserId = PlaceholderIds.UserId,
            GroupId = group.Id,
            Role = GroupRole.Teacher
        });
        await db.SaveChangesAsync();
        return (group.Id, course.Id);
    }

    private async Task<CreatedResponse> PostAsync(string path, object body)
    {
        var response = await _client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>())!;
    }
}
