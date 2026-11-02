using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class CourseCopyEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private InMemoryFileStorage Storage => (InMemoryFileStorage)factory.Services.GetRequiredService<IFileStorage>();

    private record ModuleResponse(Guid Id, Guid CourseId, string Title, int Position, DateTimeOffset? PublishAt);

    private record CopyResponse(List<ModuleResponse> Modules, int PartCount);

    // Källkursen: vecka 1 med teori (bilaga), inlämning (låst av teorin) och vecka 2 med en övning.
    // Veckorna har ett beroende över veckogränsen, som inte ska följa med.
    private record Source(
        Guid CourseId,
        Guid Week1,
        Guid Week2,
        Guid Theory,
        Guid Assignment,
        Guid Exercise,
        Guid AttachmentContentId);

    private static readonly DateTimeOffset _due = new(2026, 2, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Copy_CreatesWeeksAtTheEndWithPartsInOrder_AsDrafts()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync();

        var response = await CopyAsync(target, source.CourseId, source.Week1, source.Week2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CopyResponse>())!;
        Assert.Equal(3, result.PartCount);
        Assert.Equal(
            [("Vecka 1", 2), ("Vecka 2", 3)],
            result.Modules.Select(module => (module.Title, module.Position)));
        await using var db = OpenDb();
        var weeks = await db.Modules.Where(module => module.CourseId == target).OrderBy(module => module.Position)
            .ToListAsync();
        Assert.Equal(["Befintlig", "Vecka 1", "Vecka 2"], weeks.Select(module => module.Title));
        var copiedParts = await PartsOfAsync(db, weeks[1].Id);
        Assert.Equal(["Teori", "Inlämning"], copiedParts.Select(part => part.Content.Title));
        Assert.Equal([1, 2], copiedParts.Select(part => part.Link.Position));
        Assert.All(copiedParts, part => Assert.Equal(ContentStatus.Draft, part.Content.Status));
        Assert.All(copiedParts, part => Assert.Equal(target, part.Link.CourseId));
        var assignment = copiedParts[1].Content.Assignment!;
        Assert.Equal(GradeScale.PassFail, assignment.GradeScale);
        Assert.Equal([SubmissionMode.Text], assignment.SubmissionModes);
        Assert.Equal(CompletionRule.Approved, assignment.CompletionRule);
    }

    [Fact]
    public async Task Copy_RemapsDependenciesToTheNewParts_AndSkipsCrossWeekOnes()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync();

        await CopyAsync(target, source.CourseId, source.Week1, source.Week2);

        await using var db = OpenDb();
        var copiedWeeks = await db.Modules.Where(module => module.CourseId == target && module.Title != "Befintlig")
            .OrderBy(module => module.Position).ToListAsync();
        var copiedLinks = await db.ContentLinks.Where(link => link.CourseId == target && link.ModuleId != null
            && copiedWeeks.Select(week => week.Id).Contains(link.ModuleId.Value)).ToListAsync();
        var dependencies = await db.ContentLinkDependencies
            .Where(dependency => copiedLinks.Select(link => link.Id).Contains(dependency.BlockedLinkId)
                || copiedLinks.Select(link => link.Id).Contains(dependency.BlockingLinkId))
            .ToListAsync();
        var dependency = Assert.Single(dependencies);
        var contentTitles = await db.Content.ToDictionaryAsync(content => content.Id, content => content.Title);
        Assert.Equal(
            "Inlämning",
            contentTitles[copiedLinks.Single(link => link.Id == dependency.BlockedLinkId).ContentId]);
        Assert.Equal(
            "Teori",
            contentTitles[copiedLinks.Single(link => link.Id == dependency.BlockingLinkId).ContentId]);
        Assert.DoesNotContain(dependencies, found => found.BlockedLinkId == source.Exercise);
        var originalLinkIds = await db.ContentLinks.Where(link => link.CourseId == source.CourseId)
            .Select(link => link.Id).ToListAsync();
        Assert.DoesNotContain(copiedLinks, link => originalLinkIds.Contains(link.Id));
    }

    [Fact]
    public async Task Copy_NeverCopiesSubmissionsGradesOrProgress()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync();

        await CopyAsync(target, source.CourseId, source.Week1, source.Week2);

        await using var db = OpenDb();
        var copiedLinkIds = await db.ContentLinks.Where(link => link.CourseId == target)
            .Select(link => link.Id).ToListAsync();
        Assert.Empty(await db.Submissions.Where(submission => copiedLinkIds.Contains(submission.ContentLinkId))
            .ToListAsync());
        Assert.Empty(await db.PartCompletions.Where(completion => copiedLinkIds.Contains(completion.ContentLinkId))
            .ToListAsync());
        Assert.Equal(1, await db.Submissions.CountAsync(submission => submission.ContentLinkId == source.Assignment));
    }

    [Fact]
    public async Task Copy_ShiftsDatesByTheDifferenceBetweenTheCourseStartDates()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync(startsOn: new DateOnly(2026, 2, 2));

        await CopyAsync(target, source.CourseId, source.Week1);

        await using var db = OpenDb();
        var week = await db.Modules.SingleAsync(module => module.CourseId == target && module.Title == "Vecka 1");
        var copiedAssignment = (await PartsOfAsync(db, week.Id)).Single(part => part.Content.Title == "Inlämning");
        // Källan startar 2026-01-05, målet 2026-02-02: 28 dagar.
        Assert.Equal(_due.AddDays(28), copiedAssignment.Link.DueAt);
        Assert.Equal([_due.AddDays(35), _due.AddDays(42)], copiedAssignment.Link.ResubmitDueAt);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero).AddDays(28), week.PublishAt);
        var original = await db.ContentLinks.SingleAsync(link => link.Id == source.Assignment);
        Assert.Equal(_due, original.DueAt);
    }

    [Fact]
    public async Task Copy_WithoutStartDates_KeepsTheDates()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync(startsOn: null);

        await CopyAsync(target, source.CourseId, source.Week1);

        await using var db = OpenDb();
        var week = await db.Modules.SingleAsync(module => module.CourseId == target && module.Title == "Vecka 1");
        Assert.Equal(
            _due,
            (await PartsOfAsync(db, week.Id)).Single(part => part.Content.Title == "Inlämning").Link.DueAt);
    }

    [Fact]
    public async Task Copy_GivesIndependentContent_SoEditingTheCopyLeavesTheSourceAlone()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync();
        await CopyAsync(target, source.CourseId, source.Week1);
        await using var db = OpenDb();
        var week = await db.Modules.SingleAsync(module => module.CourseId == target && module.Title == "Vecka 1");
        var copy = (await PartsOfAsync(db, week.Id)).Single(part => part.Content.Title == "Teori");

        var edit = await _client.PutAsJsonAsync(
            $"/api/content/{copy.Content.Id}", new { title = "Ändrad kopia", bodyMd = "Ny text" });

        edit.EnsureSuccessStatusCode();
        await using var check = OpenDb();
        var original = await check.ContentLinks.SingleAsync(link => link.Id == source.Theory);
        var originalContent = await check.Content.SingleAsync(content => content.Id == original.ContentId);
        Assert.NotEqual(copy.Content.Id, originalContent.Id);
        Assert.Equal("Teori", originalContent.Title);
    }

    [Fact]
    public async Task Copy_GivesTheCopyItsOwnAttachmentFile_SoItSurvivesDeletingTheSource()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync();
        await CopyAsync(target, source.CourseId, source.Week1);
        await using var db = OpenDb();
        var week = await db.Modules.SingleAsync(module => module.CourseId == target && module.Title == "Vecka 1");
        var copy = (await PartsOfAsync(db, week.Id)).Single(part => part.Content.Title == "Teori");
        var copiedAttachment = await db.Attachments.SingleAsync(attachment => attachment.ContentId == copy.Content.Id);
        var originalAttachment = await db.Attachments.SingleAsync(
            attachment => attachment.ContentId == source.AttachmentContentId);

        // Elevarbete stoppar radering av veckan, och det är källkursens elever, inte kopians.
        await using (var cleanup = OpenDb())
        {
            await cleanup.Submissions.Where(submission => submission.ContentLinkId == source.Assignment)
                .ExecuteDeleteAsync();
            await cleanup.PartCompletions.Where(completion => completion.ContentLinkId == source.Theory)
                .ExecuteDeleteAsync();
        }

        var deleted = await _client.DeleteAsync($"/api/courses/{source.CourseId}/modules/{source.Week1}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.NotEqual(originalAttachment.StorageKey, copiedAttachment.StorageKey);
        Assert.Equal("slides.pdf", copiedAttachment.FileName);
        Assert.StartsWith($"content/{copy.Content.Id}/", copiedAttachment.StorageKey);
        Assert.DoesNotContain(originalAttachment.StorageKey, Storage.Keys);
        Assert.Contains(copiedAttachment.StorageKey, Storage.Keys);
        var url = await factory.Services.GetRequiredService<IFileStorage>()
            .GetSignedUrlAsync(copiedAttachment.StorageKey, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Contains(copiedAttachment.StorageKey, url);
    }

    [Fact]
    public async Task Copy_AsTeacherWhoCannotTeachTheSource_IsForbidden_AndCopiesNothing()
    {
        var source = await AddSourceAsync();
        var scenario = await AddTeacherOfTargetOnlyAsync();

        var response = await CopyAsUserAsync(scenario.TargetId, source.CourseId, scenario.TeacherId, source.Week1);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = OpenDb();
        Assert.Equal(1, await db.Modules.CountAsync(module => module.CourseId == scenario.TargetId));
    }

    [Fact]
    public async Task Copy_AsTeacherOfBothCourses_Succeeds_AndAsTeacherWhoCannotTeachTheTarget_IsForbidden()
    {
        var source = await AddSourceAsync();
        var both = await AddTeacherOfTargetAndSourceAsync(source.CourseId);
        var other = await AddTeacherOfTargetOnlyAsync();

        var allowed = await CopyAsUserAsync(both.TargetId, source.CourseId, both.TeacherId, source.Week2);
        var denied = await CopyAsUserAsync(both.TargetId, source.CourseId, other.TeacherId, source.Week2);

        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Copy_WithWeeksThatAreNotInTheSourceCourse_ReturnsBadRequest()
    {
        var source = await AddSourceAsync();
        var target = await AddCourseWithWeekAsync();

        var foreign = await CopyAsync(target, source.CourseId, Guid.NewGuid());
        var none = await CopyAsync(target, source.CourseId);
        var twice = await CopyAsync(target, source.CourseId, source.Week1, source.Week1);

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        await using var db = OpenDb();
        Assert.Equal(1, await db.Modules.CountAsync(module => module.CourseId == target));
    }

    [Fact]
    public async Task Copy_FromOrToACourseThatDoesNotExist_ReturnsNotFound()
    {
        var source = await AddSourceAsync();

        var missingTarget = await CopyAsync(Guid.NewGuid(), source.CourseId, source.Week1);
        var missingSource = await CopyAsync(source.CourseId, Guid.NewGuid(), source.Week1);

        Assert.Equal(HttpStatusCode.NotFound, missingTarget.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingSource.StatusCode);
    }

    private Task<HttpResponseMessage> CopyAsync(Guid targetId, Guid sourceId, params Guid[] moduleIds) =>
        CopyAsUserAsync(targetId, sourceId, Guid.Empty, moduleIds);

    // Guid.Empty betyder placeholder-användaren, som är Admin.
    private async Task<HttpResponseMessage> CopyAsUserAsync(
        Guid targetId, Guid sourceId, Guid asUser, params Guid[] moduleIds)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/courses/{targetId}/modules/copy")
        {
            Content = JsonContent.Create(new { sourceCourseId = sourceId, moduleIds })
        };
        if (asUser != Guid.Empty)
        {
            request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        }

        return await _client.SendAsync(request);
    }

    private AppDb OpenDb() => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDb>();

    private static async Task<List<LinkedContentRow>> PartsOfAsync(AppDb db, Guid moduleId)
    {
        var links = await db.ContentLinks.Where(link => link.ModuleId == moduleId)
            .OrderBy(link => link.Position).ToListAsync();
        var rows = new List<LinkedContentRow>();
        foreach (var link in links)
        {
            var content = await db.Content.Include(found => found.Assignment)
                .SingleAsync(found => found.Id == link.ContentId);
            rows.Add(new LinkedContentRow(link, content));
        }

        return rows;
    }

    private record LinkedContentRow(ContentLink Link, Content Content);

    private Task<Guid> AddCourseWithWeekAsync() => AddCourseWithWeekAsync(new DateOnly(2026, 1, 5));

    private async Task<Guid> AddCourseWithWeekAsync(DateOnly? startsOn)
    {
        await using var db = OpenDb();
        var school = await TestData.AddSchoolAsync(db);
        var course = await TestData.AddCourseAsync(db, school.Id);
        course.StartsOn = startsOn;
        db.Modules.Add(new Module { CourseId = course.Id, Title = "Befintlig", Position = 1 });
        await db.SaveChangesAsync();
        return course.Id;
    }

    private async Task<Source> AddSourceAsync()
    {
        await using var db = OpenDb();
        var school = await TestData.AddSchoolAsync(db);
        var author = await TestData.AddUserAsync(db, school.Id, UserRole.Teacher);
        var student = await TestData.AddUserAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id);
        course.StartsOn = new DateOnly(2026, 1, 5);
        var week1 = new Module
        {
            CourseId = course.Id,
            Title = "Vecka 1",
            Description = "Start",
            Position = 1,
            PublishAt = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero)
        };
        var week2 = new Module { CourseId = course.Id, Title = "Vecka 2", Position = 2 };
        db.Modules.AddRange(week1, week2);

        var theory = new Content { AuthorId = author.Id, Type = ContentType.Theory, Title = "Teori", BodyMd = "Text" };
        var assignment = new Content
        {
            AuthorId = author.Id,
            Type = ContentType.Assignment,
            Title = "Inlämning",
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFail,
                SubmissionModes = [SubmissionMode.Text],
                CompletionRule = CompletionRule.Approved
            }
        };
        var exercise = new Content { AuthorId = author.Id, Type = ContentType.Theory, Title = "Övning" };
        db.Content.AddRange(theory, assignment, exercise);

        var theoryLink = Link(theory, course.Id, week1.Id, 1);
        var assignmentLink = Link(assignment, course.Id, week1.Id, 2);
        assignmentLink.DueAt = _due;
        assignmentLink.ResubmitDueAt = [_due.AddDays(7), _due.AddDays(14)];
        var exerciseLink = Link(exercise, course.Id, week2.Id, 1);
        db.ContentLinks.AddRange(theoryLink, assignmentLink, exerciseLink);
        db.ContentLinkDependencies.AddRange(
            new ContentLinkDependency { BlockedLinkId = assignmentLink.Id, BlockingLinkId = theoryLink.Id },
            // Över veckogränsen (går inte att skapa via API:et men ska ändå aldrig kopieras).
            new ContentLinkDependency { BlockedLinkId = exerciseLink.Id, BlockingLinkId = theoryLink.Id });
        db.Submissions.Add(new Submission
        {
            ContentLinkId = assignmentLink.Id,
            StudentId = student.Id,
            Status = SubmissionStatus.Submitted,
            SubmittedAt = _due
        });
        db.PartCompletions.Add(new PartCompletion { ContentLinkId = theoryLink.Id, StudentId = student.Id });
        var key = await Storage.UploadAsync($"content/{theory.Id}", new MemoryStream([1, 2, 3]), "application/pdf",
            CancellationToken.None);
        db.Attachments.Add(new Attachment
        {
            ContentId = theory.Id,
            FileName = "slides.pdf",
            StorageKey = key,
            SizeBytes = 3
        });
        await db.SaveChangesAsync();
        return new Source(
            course.Id, week1.Id, week2.Id, theoryLink.Id, assignmentLink.Id, exerciseLink.Id, theory.Id);
    }

    private static ContentLink Link(Content content, Guid courseId, Guid moduleId, int position) =>
        new() { ContentId = content.Id, CourseId = courseId, ModuleId = moduleId, Position = position };

    private record TeacherScenario(Guid TeacherId, Guid TargetId);

    private async Task<TeacherScenario> AddTeacherOfTargetOnlyAsync()
    {
        await using var db = OpenDb();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
        db.Modules.Add(new Module { CourseId = course.Id, Title = "Befintlig", Position = 1 });
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = teacher.Id });
        await db.SaveChangesAsync();
        return new TeacherScenario(teacher.Id, course.Id);
    }

    private async Task<TeacherScenario> AddTeacherOfTargetAndSourceAsync(Guid sourceCourseId)
    {
        var scenario = await AddTeacherOfTargetOnlyAsync();
        await using var db = OpenDb();
        var groupId = await db.GroupCourses.Where(groupCourse => groupCourse.CourseId == scenario.TargetId)
            .Select(groupCourse => groupCourse.GroupId).SingleAsync();
        db.GroupCourses.Add(new GroupCourse { GroupId = groupId, CourseId = sourceCourseId });
        db.CourseTeachers.Add(new CourseTeacher { CourseId = sourceCourseId, UserId = scenario.TeacherId });
        await db.SaveChangesAsync();
        return scenario;
    }
}
