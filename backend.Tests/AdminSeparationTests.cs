using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Admin bestämmer vem som undervisar vad, läraren bestämmer vad som händer i kursen. Admin har ingen genväg in i
// undervisningen och får inte läsa inlämningar eller betyg. Eleven läser sina egna inlämningar, men bedömer inte. En admin som också
// undervisar får lärarrätt genom en rad i CourseTeachers, inte genom sin roll.
public class AdminSeparationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Scenario(
        Guid SchoolId,
        Guid ClassA,
        Guid ClassB,
        Guid CourseId,
        Guid OtherCourseId,
        Guid TeacherId,
        Guid AdminId,
        Guid AdminTeacherId,
        Guid StudentA,
        Guid StudentB,
        Guid ClassTeacherId,
        Guid OutsideTeacherId,
        Guid OtherSchoolAdminId,
        Guid ModuleId,
        Guid PublishedContentId,
        Guid DraftContentId,
        Guid AssignmentContentId,
        Guid AssignmentLinkId,
        Guid SubmissionA,
        Guid SubmissionB);

    private record ModuleResponse(Guid Id, List<ModuleItem> Content);

    private record ModuleItem(Guid LinkId, Guid ContentId, string Title, string Status, int? SubmittedCount);

    private record SubmissionResponse(Guid Id, Guid StudentId, string Status, GradeResponse? Grade);

    private record GradeResponse(string? Level, string Feedback);

    private record FeedItem(Guid ContentId, string Title);

    private record CourseResponse(
        Guid Id,
        string Title,
        string Code,
        string Description,
        int? Points,
        string? StartsOn);

    private record OverviewResponse(
        List<ClassTimeline> ClassTimelines,
        List<CourseWithoutTeacher> CoursesWithoutTeacher,
        List<UserWithoutPassword> UsersWithoutPassword);

    private record ClassTimeline(Guid GroupId, string Name, List<ClassCourse> CurrentCourses);

    private record ClassCourse(Guid CourseId, string Title, string Code, string? StartsOn, string? EndsOn);

    private record CourseWithoutTeacher(Guid CourseId, string Code, Guid? GroupId);

    private record UserWithoutPassword(Guid UserId, string Name, Guid? GroupId);

    private record AnalyticsResponse(
        Guid GroupId,
        string GroupName,
        List<AnalyticsCourse> Courses,
        List<AnalyticsStudent> Students);

    private record AnalyticsCourse(
        Guid CourseId,
        int StudentCount,
        int AssignmentCount,
        int GradedCount,
        int PassCount,
        int NeedsWorkCount,
        int AtRiskStudentCount);

    private record AnalyticsStudent(Guid StudentId, bool NeedsSupport, List<AnalyticsStudentCourse> Courses);

    private record AnalyticsStudentCourse(Guid CourseId, int PassCount, int NeedsWorkCount, bool NeedsSupport);

    // --- Admin undervisar inte ---

    [Fact]
    public async Task Admin_CannotEditPublishLinkOrAttachToContent_ButAnAdminWithATeacherRowCan()
    {
        var scenario = await AddScenarioAsync();
        var contentId = scenario.PublishedContentId;

        var update = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}", scenario.AdminId, new { title = "Ändrad", bodyMd = "" });
        var status = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}/status", scenario.AdminId, new { status = "draft" });
        var link = await SendAsync(
            HttpMethod.Post, $"/api/content/{contentId}/links", scenario.AdminId, new { courseId = scenario.CourseId });
        var attachment = await UploadAsync(contentId, scenario.AdminId);

        var byTeacherRow = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}", scenario
                .AdminTeacherId, new { title = "Ändrad", bodyMd = "" });

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, link.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, attachment.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byTeacherRow.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotChangeWeeksOrRoadmap_ButAnAdminWithATeacherRowCan()
    {
        var scenario = await AddScenarioAsync();
        var path = $"/api/courses/{scenario.CourseId}/modules/{scenario.ModuleId}";

        var rename = await SendAsync(HttpMethod.Put, path, scenario.AdminId, new { title = "Ny" });
        var delete = await SendAsync(HttpMethod.Delete, path, scenario.AdminId);
        var roadmap = await SendAsync(
            HttpMethod.Put,
            $"{path}/roadmap",
            scenario.AdminId,
            new { links = new object[0] });

        var byTeacherRow = await SendAsync(HttpMethod.Put, path, scenario.AdminTeacherId, new { title = "Ny" });

        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, roadmap.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byTeacherRow.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotReadSubmissionsOrGrades_ButAnAdminWithATeacherRowCan()
    {
        var scenario = await AddScenarioAsync();

        var list = await SendAsync(
            HttpMethod.Get,
            $"/api/links/{scenario.AssignmentLinkId}/submissions",
            scenario.AdminId);
        var single = await SendAsync(HttpMethod.Get, $"/api/submissions/{scenario.SubmissionA}", scenario.AdminId);
        var grade = await SendAsync(
            HttpMethod.Post,
            $"/api/submissions/{scenario.SubmissionA}/grade",
            scenario.AdminId,
            new { level = "pass", feedback = "Bra" });

        var listed = await GetAsync<List<SubmissionResponse>>(
            $"/api/links/{scenario.AssignmentLinkId}/submissions", scenario.AdminTeacherId);
        var graded = await SendAsync(
            HttpMethod.Post,
            $"/api/submissions/{scenario.SubmissionA}/grade",
            scenario.AdminTeacherId,
            new { level = "pass", feedback = "Bra" });

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, single.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, grade.StatusCode);
        Assert.Equal(2, listed.Count);
        Assert.Equal(HttpStatusCode.OK, graded.StatusCode);
    }

    [Fact]
    public async Task Admin_GetsNoSubmissionCountsInTheCourseContent_ButTheTeacherDoes()
    {
        var scenario = await AddScenarioAsync();

        var asAdmin = await GetAsync<List<ModuleResponse>>(
            $"/api/courses/{scenario.CourseId}/modules",
            scenario.AdminId);
        var asTeacher = await GetAsync<List<ModuleResponse>>(
            $"/api/courses/{scenario.CourseId}/modules", scenario.TeacherId);

        var adminItem = asAdmin.Single().Content.Single(item => item.ContentId == scenario.AssignmentContentId);
        var teacherItem = asTeacher.Single().Content.Single(item => item.ContentId == scenario.AssignmentContentId);
        Assert.Null(adminItem.SubmittedCount);
        Assert.NotNull(teacherItem.SubmittedCount);
    }

    [Fact]
    public async Task CreateContent_IsForTheOneWhoTeachesSomething_NotForTheGlobalTeacherRole()
    {
        var scenario = await AddScenarioAsync();
        Guid roleOnlyTeacher;
        await using (var db = OpenDb())
        {
            roleOnlyTeacher = (await TestData.AddUserAsync(db, scenario.SchoolId, UserRole.Teacher)).Id;
        }

        var content = new { type = "theory", title = "Teori", bodyMd = "" };
        var byCourseTeacher = await SendAsync(HttpMethod.Post, "/api/content", scenario.TeacherId, content);
        var byClassTeacher = await SendAsync(HttpMethod.Post, "/api/content", scenario.ClassTeacherId, content);
        var byRoleOnly = await SendAsync(HttpMethod.Post, "/api/content", roleOnlyTeacher, content);
        var byAdmin = await SendAsync(HttpMethod.Post, "/api/content", scenario.AdminId, content);

        Assert.Equal(HttpStatusCode.Created, byCourseTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byClassTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byRoleOnly.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
    }

    // --- Admin ändrar kursen, inte beskrivningen ---

    [Fact]
    public async Task UpdateCourse_ByAdminWhoDoesNotTeach_ChangesTheFactsButKeepsTheDescription()
    {
        var scenario = await AddScenarioAsync();
        await using (var db = OpenDb())
        {
            (await db.Courses.SingleAsync(course => course.Id == scenario.CourseId)).Description = "Lärarens text";
            await db.SaveChangesAsync();
        }

        var response = await SendAsync(HttpMethod.Put, $"/api/courses/{scenario.CourseId}", scenario.AdminId, new
        {
            title = "Nytt namn",
            description = "Adminens text",
            points = 30,
            startsOn = "2027-01-11",
            endsOn = "2027-03-12"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var course = (await response.Content.ReadFromJsonAsync<CourseResponse>())!;
        Assert.Equal(
            ("Nytt namn", "Lärarens text", 30, "2027-01-11"),
            (course.Title, course.Description, course.Points, course.StartsOn));
    }

    [Fact]
    public async Task UpdateCourse_ByAdminWhoTeaches_AlsoChangesTheDescription()
    {
        var scenario = await AddScenarioAsync();

        var response = await SendAsync(HttpMethod.Put, $"/api/courses/{scenario.CourseId}", scenario.AdminTeacherId, new
        {
            title = "Nytt namn",
            description = "Min text",
            points = (int?)null,
            startsOn = (string?)null,
            endsOn = (string?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Min text", (await response.Content.ReadFromJsonAsync<CourseResponse>())!.Description);
    }

    [Fact]
    public async Task UpdateCourse_ByAdminOfAnotherSchoolOrTeacherOfAnotherCourse_IsRefused()
    {
        var scenario = await AddScenarioAsync();
        var body = new
        {
            title = "X",
            description = "",
            points = (int?)null,
            startsOn = (string?)null,
            endsOn = (string?)null
        };

        var otherSchool = await SendAsync(
            HttpMethod.Put,
            $"/api/courses/{scenario.CourseId}",
            scenario.OtherSchoolAdminId,
            body);
        var outsideTeacher = await SendAsync(
            HttpMethod.Put,
            $"/api/courses/{scenario.CourseId}",
            scenario.OutsideTeacherId,
            body);

        Assert.Equal(HttpStatusCode.NotFound, otherSchool.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsideTeacher.StatusCode);
    }

    // --- Eleven läser sin egen inlämning men bedömer inte ---

    [Fact]
    public async Task Student_ReadsOnlyTheirOwnSubmissionAndGrade_ButCannotGrade()
    {
        var scenario = await AddScenarioAsync();
        await using (var db = OpenDb())
        {
            db.Grades.Add(new Grade
            {
                SubmissionId = scenario.SubmissionA,
                GradedById = scenario.TeacherId,
                Level = GradeLevel.Pass,
                Feedback = "Bra"
            });
            await db.SaveChangesAsync();
        }

        var list = await GetAsync<List<SubmissionResponse>>(
            $"/api/links/{scenario.AssignmentLinkId}/submissions", scenario.StudentA);
        var own = await GetAsync<SubmissionResponse>($"/api/submissions/{scenario.SubmissionA}", scenario.StudentA);
        var other = await SendAsync(HttpMethod.Get, $"/api/submissions/{scenario.SubmissionB}", scenario.StudentA);
        var grade = await SendAsync(
            HttpMethod.Post,
            $"/api/submissions/{scenario.SubmissionA}/grade",
            scenario.StudentA,
            new { level = "distinction", feedback = "Toppen" });

        Assert.Equal([scenario.StudentA], list.Select(submission => submission.StudentId));
        Assert.Equal("Pass", own.Grade!.Level);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, grade.StatusCode);
    }

    [Fact]
    public async Task Student_ReadsPublishedContentOfTheClassCourse_ButNotDrafts_AndCannotPostNews()
    {
        var scenario = await AddScenarioAsync();

        var weeks = await GetAsync<List<ModuleResponse>>(
            $"/api/courses/{scenario.CourseId}/modules",
            scenario.StudentA);
        var feed = await GetAsync<List<FeedItem>>($"/api/courses/{scenario.CourseId}/content", scenario.StudentA);
        var draft = await SendAsync(HttpMethod.Get, $"/api/content/{scenario.DraftContentId}", scenario.StudentA);
        var news = await SendAsync(HttpMethod.Post, "/api/content/with-link", scenario.StudentA, new
        {
            content = new { type = "news", title = "Nyhet", bodyMd = "Text" },
            link = new { groupId = scenario.ClassA }
        });

        Assert.DoesNotContain(weeks.Single().Content, item => item.ContentId == scenario.DraftContentId);
        Assert.Contains(weeks.Single().Content, item => item.ContentId == scenario.PublishedContentId);
        Assert.Contains(feed, item => item.ContentId == scenario.PublishedContentId);
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, news.StatusCode);
    }

    // --- Läsa kursens och gruppens innehåll ---

    [Fact]
    public async Task CourseContent_CanBeReadByTheCoursePeopleAndAdminOfTheSchool_Only()
    {
        var scenario = await AddScenarioAsync();
        var path = $"/api/courses/{scenario.CourseId}/content";

        foreach (var user in new[] { scenario.TeacherId, scenario.StudentA, scenario.AdminId })
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, path, user)).StatusCode);
        }

        foreach (var user in new[] { scenario.OutsideTeacherId, scenario.OtherSchoolAdminId, scenario.ClassTeacherId })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, path, user)).StatusCode);
        }
    }

    [Fact]
    public async Task GroupContent_CanBeReadByMembersTeachersOfTheGroupsCoursesAndAdmin_Only()
    {
        var scenario = await AddScenarioAsync();
        var path = $"/api/groups/{scenario.ClassA}/content";

        foreach (var user in new[] {
            scenario.StudentA,
            scenario.ClassTeacherId,
            scenario.TeacherId,
            scenario.AdminId })
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, path, user)).StatusCode);
        }

        foreach (var user in new[] { scenario.StudentB, scenario.OutsideTeacherId, scenario.OtherSchoolAdminId })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, path, user)).StatusCode);
        }
    }

    [Fact]
    public async Task CourseWeeks_AreForbiddenForAnyoneNotConnectedToTheCourse()
    {
        var scenario = await AddScenarioAsync();
        var path = $"/api/courses/{scenario.OtherCourseId}/modules";

        var student = await SendAsync(HttpMethod.Get, path, scenario.StudentA);
        var outsider = await SendAsync(HttpMethod.Get, path, scenario.OtherSchoolAdminId);

        Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
    }

    [Fact]
    public async Task Draft_IsHiddenFromStudents_ButReadableByTheTeacherAndReadOnlyForAdmin()
    {
        var scenario = await AddScenarioAsync();
        var path = $"/api/courses/{scenario.CourseId}/modules";

        foreach (var user in new[] { scenario.StudentA })
        {
            var weeks = await GetAsync<List<ModuleResponse>>(path, user);
            Assert.DoesNotContain(weeks.Single().Content, item => item.ContentId == scenario.DraftContentId);
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await SendAsync(HttpMethod.Get, $"/api/content/{scenario.DraftContentId}", user)).StatusCode);
            Assert.Empty(
                await GetAsync<List<CommentResponse>>($"/api/content/{scenario.DraftContentId}/comments", user));
        }

        foreach (var user in new[] { scenario.TeacherId, scenario.AdminId })
        {
            var weeks = await GetAsync<List<ModuleResponse>>(path, user);
            Assert.Contains(weeks.Single().Content, item => item.ContentId == scenario.DraftContentId);
            Assert.Equal(
                HttpStatusCode.OK,
                (await SendAsync(HttpMethod.Get, $"/api/content/{scenario.DraftContentId}", user)).StatusCode);
        }
    }

    private record CommentResponse(Guid Id);

    // --- Adminens översikt ---

    [Fact]
    public async Task AdminOverview_ListsWhatNeedsAttention_ForAdminOnly()
    {
        var scenario = await AddScenarioAsync();
        Guid emptyClass;
        Guid noPassword;
        await using (var db = OpenDb())
        {
            var group = await TestData.AddGroupAsync(db, scenario.SchoolId);
            emptyClass = group.Id;
            var student = await TestData.AddMemberAsync(db, group);
            noPassword = student.Id;
        }

        var overview = await GetAsync<OverviewResponse>("/api/me/admin-overview", scenario.AdminId);
        var teacher = await SendAsync(HttpMethod.Get, "/api/me/admin-overview", scenario.TeacherId);
        var student2 = await SendAsync(HttpMethod.Get, "/api/me/admin-overview", scenario.StudentA);

        Assert.Contains(overview.UsersWithoutPassword, user => user.UserId == noPassword && user.GroupId == emptyClass);
        Assert.DoesNotContain(overview.CoursesWithoutTeacher, course => course.CourseId == scenario.CourseId);
        Assert.Contains(overview.CoursesWithoutTeacher, course => course.CourseId == scenario.OtherCourseId
            && course.GroupId == scenario.ClassB);
        Assert.Equal(HttpStatusCode.Forbidden, teacher.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, student2.StatusCode);
    }

    [Fact]
    public async Task AdminOverview_SkipsFinishedCoursesAndClasses_ButKeepsUndatedOnes()
    {
        var scenario = await AddScenarioAsync();
        Guid finishedCourse;
        Guid undatedCourse;
        await using (var db = OpenDb())
        {
            var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);
            var finished = await TestData.AddGroupAsync(db, scenario.SchoolId);
            var mixed = await TestData.AddGroupAsync(db, scenario.SchoolId);
            var old = await TestData.AddCourseAsync(db, scenario.SchoolId, finished.Id, placeholderTeaches: false);
            var undated = await TestData.AddCourseAsync(db, scenario.SchoolId, mixed.Id, placeholderTeaches: false);
            db.GroupCourses.Add(new GroupCourse { GroupId = mixed.Id, CourseId = old.Id });
            old.EndsOn = past;
            await db.SaveChangesAsync();
            finishedCourse = old.Id;
            undatedCourse = undated.Id;
        }

        var overview = await GetAsync<OverviewResponse>("/api/me/admin-overview", scenario.AdminId);

        Assert.DoesNotContain(overview.CoursesWithoutTeacher, course => course.CourseId == finishedCourse);
        Assert.Contains(overview.CoursesWithoutTeacher, course => course.CourseId == undatedCourse);
    }

    [Fact]
    public async Task AdminOverview_ReturnsCurrentCourseTimelineForClasses()
    {
        var scenario = await AddScenarioAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using (var db = OpenDb())
        {
            var course = await db.Courses.SingleAsync(row => row.Id == scenario.CourseId);
            course.StartsOn = today.AddDays(-10);
            course.EndsOn = today.AddDays(10);
            var other = await db.Courses.SingleAsync(row => row.Id == scenario.OtherCourseId);
            other.StartsOn = today.AddDays(20);
            other.EndsOn = today.AddDays(50);
            await db.SaveChangesAsync();
        }

        var overview = await GetAsync<OverviewResponse>("/api/me/admin-overview", scenario.AdminId);

        var classA = overview.ClassTimelines.Single(group => group.GroupId == scenario.ClassA);
        var current = classA.CurrentCourses.Single();
        Assert.Equal(scenario.CourseId, current.CourseId);
        Assert.Equal(today.AddDays(-10).ToString("yyyy-MM-dd"), current.StartsOn);
        Assert.Equal(today.AddDays(10).ToString("yyyy-MM-dd"), current.EndsOn);

        var classB = overview.ClassTimelines.Single(group => group.GroupId == scenario.ClassB);
        Assert.Contains(classB.CurrentCourses, course => course.CourseId == scenario.CourseId);
        Assert.DoesNotContain(classB.CurrentCourses, course => course.CourseId == scenario.OtherCourseId);
    }

    [Fact]
    public async Task GroupAnalytics_ShowsGradesAndSupportPerStudent_ForAdminOnly()
    {
        var scenario = await AddScenarioAsync();
        Guid supportingStudent;
        await using (var db = OpenDb())
        {
            var submission = await db.Submissions.SingleAsync(row => row.Id == scenario.SubmissionA);
            submission.Status = SubmissionStatus.Graded;
            db.Grades.Add(new Grade
            {
                SubmissionId = submission.Id,
                GradedById = scenario.TeacherId,
                Level = GradeLevel.Pass,
                Feedback = "Bra"
            });
            var group = await db.Groups.SingleAsync(candidate => candidate.Id == scenario.ClassA);
            var extra = await TestData.AddMemberAsync(db, group);
            supportingStudent = extra.Id;
            db.Submissions.Add(new Submission
            {
                ContentLinkId = scenario.AssignmentLinkId,
                StudentId = extra.Id,
                Status = SubmissionStatus.NeedsWork
            });
            await db.SaveChangesAsync();
        }

        var analytics = await GetAsync<AnalyticsResponse>(
            $"/api/groups/{scenario.ClassA}/analytics", scenario.AdminId);

        Assert.Equal(scenario.ClassA, analytics.GroupId);
        var course = analytics.Courses.Single(item => item.CourseId == scenario.CourseId);
        Assert.Equal(2, course.StudentCount);
        Assert.Equal(1, course.AssignmentCount);
        Assert.Equal(1, course.GradedCount);
        Assert.Equal(1, course.PassCount);
        Assert.Equal(1, course.NeedsWorkCount);
        Assert.Equal(1, course.AtRiskStudentCount);

        var passStudent = analytics.Students.Single(student => student.StudentId == scenario.StudentA);
        Assert.False(passStudent.NeedsSupport);
        Assert.Equal(1, passStudent.Courses.Single().PassCount);
        var needsWorkStudent = analytics.Students.Single(student => student.StudentId == supportingStudent);
        Assert.True(needsWorkStudent.NeedsSupport);
        Assert.Equal(1, needsWorkStudent.Courses.Single().NeedsWorkCount);

        var path = $"/api/groups/{scenario.ClassA}/analytics";
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, path, scenario.TeacherId)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(HttpMethod.Get, path, scenario.StudentA)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, path, scenario.OtherSchoolAdminId)).StatusCode);
    }

    // --- Hjälpmetoder ---

    private AppDb OpenDb() => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDb>();

    private async Task<T> GetAsync<T>(string url, Guid asUser)
    {
        var response = await SendAsync(HttpMethod.Get, url, asUser);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, Guid asUser, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> UploadAsync(Guid contentId, Guid asUser)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "slides.pdf");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/content/{contentId}/attachments")
        {
            Content = form
        };
        request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        return await _client.SendAsync(request);
    }

    // Kursen läses av två klasser. Kursen har en vecka med en publicerad del,
    // ett utkast och en inlämning som båda klassernas elever har lämnat in. Den andra kursen läses av klass B och
    // saknar lärare.
    private async Task<Scenario> AddScenarioAsync()
    {
        await using var db = OpenDb();
        var school = await db.Schools.SingleAsync(found => found.Id == PlaceholderIds.SchoolId);
        var classA = await TestData.AddGroupAsync(db, school.Id);
        var classB = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, classA.Id, placeholderTeaches: false);
        db.GroupCourses.Add(new GroupCourse { GroupId = classB.Id, CourseId = course.Id });
        var otherCourse = await TestData.AddCourseAsync(db, school.Id, classB.Id, placeholderTeaches: false);

        var teacher = await TestData.AddCourseTeacherAsync(db, course);
        var outsideTeacher = await TestData.AddCourseTeacherAsync(db, await TestData.AddCourseAsync(
            db, school.Id, placeholderTeaches: false));
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        var adminTeacher = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        await TestData.AddCourseTeacherAsync(db, course, adminTeacher);
        var otherSchoolAdmin = await TestData.AddUserAsync(db, role: UserRole.Admin);
        var classTeacher = await TestData.AddMemberAsync(db, classA, GroupRole.Teacher, UserRole.Teacher);
        var studentA = await TestData.AddMemberAsync(db, classA);
        var studentB = await TestData.AddMemberAsync(db, classB);

        var week = new Module { CourseId = course.Id, Title = "Vecka 1", Position = 1 };
        db.Modules.Add(week);
        var published = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Theory,
            Title = "Publicerad",
            Status = ContentStatus.Published
        };
        var draft = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Theory,
            Title = "Utkast",
            Status = ContentStatus.Draft
        };
        var assignment = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Assignment,
            Title = "Inlämning",
            Status = ContentStatus.Published,
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFail,
                SubmissionModes = [SubmissionMode.Text]
            }
        };
        db.Content.AddRange(published, draft, assignment);
        var past = DateTimeOffset.UtcNow.AddDays(-1);
        db.ContentLinks.AddRange(
            new ContentLink
            {
                ContentId = published.Id,
                CourseId = course.Id,
                ModuleId = week.Id,
                Position = 1,
                PublishAt = past
            },
            new ContentLink
            {
                ContentId = draft.Id,
                CourseId = course.Id,
                ModuleId = week.Id,
                Position = 2,
                PublishAt = past
            });
        var assignmentLink = new ContentLink
        {
            ContentId = assignment.Id,
            CourseId = course.Id,
            ModuleId = week.Id,
            Position = 3,
            PublishAt = past
        };
        db.ContentLinks.Add(assignmentLink);
        var submissionA = new Submission
        {
            ContentLinkId = assignmentLink.Id,
            StudentId = studentA.Id,
            Status = SubmissionStatus.Submitted,
            SubmittedAt = past,
            BodyMd = "Svar A"
        };
        var submissionB = new Submission
        {
            ContentLinkId = assignmentLink.Id,
            StudentId = studentB.Id,
            Status = SubmissionStatus.Submitted,
            SubmittedAt = past,
            BodyMd = "Svar B"
        };
        db.Submissions.AddRange(submissionA, submissionB);
        await db.SaveChangesAsync();

        return new Scenario(
            school.Id,
            classA.Id,
            classB.Id,
            course.Id,
            otherCourse.Id,
            teacher.Id,
            admin.Id,
            adminTeacher.Id,
            studentA.Id,
            studentB.Id,
            classTeacher.Id,
            outsideTeacher.Id,
            otherSchoolAdmin.Id,
            week.Id,
            published.Id,
            draft.Id,
            assignment.Id,
            assignmentLink.Id,
            submissionA.Id,
            submissionB.Id);
    }
}
