using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class CourseAboutEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record CourseResponse(
        Guid Id,
        string Title,
        string Code,
        string Description,
        int? Points,
        DateOnly? StartsOn,
        DateOnly? EndsOn);

    private record StaffResponse(Guid UserId, string Name, string Email, List<string> Groups);

    [Fact]
    public async Task UpdateCourse_ByTeacher_SavesTheChanges()
    {
        var (course, teacher, _) = await AddCourseWithTeacherAndStudentAsync();

        var response = await PutAsync(
            course.Id,
            new
            {
                title = " DevOps ",
                description = "**Ny** beskrivning",
                points = 25,
                startsOn = "2026-10-12",
                endsOn = "2026-11-13"
            },
            teacher.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.Equal(("DevOps", "**Ny** beskrivning"), (updated!.Title, updated.Description));
        Assert.Equal(25, updated.Points);
        Assert.Equal(new DateOnly(2026, 10, 12), updated.StartsOn);
        Assert.Equal(new DateOnly(2026, 11, 13), updated.EndsOn);

        var found = await _client.GetFromJsonAsync<CourseResponse>($"/api/courses/{course.Id}");
        Assert.Equal(course.Code, found!.Code);
        Assert.Equal(("DevOps", "**Ny** beskrivning"), (found.Title, found.Description));
    }

    [Fact]
    public async Task UpdateCourse_ByAdmin_Works()
    {
        var (course, _, _) = await AddCourseWithTeacherAndStudentAsync();

        var response = await PutAsync(course.Id, new
        {
            title = "Kursen",
            description = "",
            points = (int?)null,
            startsOn = (string?)null,
            endsOn = (string?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCourse_AsStudent_ReturnsForbidden()
    {
        var (course, _, student) = await AddCourseWithTeacherAndStudentAsync();

        var response = await PutAsync(
            course.Id,
            new
            {
                title = "Elevens titel",
                description = "",
                points = (int?)null,
                startsOn = (string?)null,
                endsOn = (string?)null
            },
            student.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Du har inte behörighet.", await response.Content.ReadAsStringAsync());
        var found = await _client.GetFromJsonAsync<CourseResponse>($"/api/courses/{course.Id}");
        Assert.Equal(course.Title, found!.Title);
    }

    [Fact]
    public async Task UpdateCourse_AsTeacherOfAnotherCourse_ReturnsForbidden()
    {
        var (course, _, _) = await AddCourseWithTeacherAndStudentAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var otherGroup = await TestData.AddGroupAsync(db, course.SchoolId);
        await TestData.AddCourseAsync(db, course.SchoolId, otherGroup.Id);
        var otherTeacher = await TestData.AddMemberAsync(db, otherGroup, GroupRole.Teacher, UserRole.Teacher);

        var response = await PutAsync(
            course.Id,
            new
            {
                title = "Annan lärares titel",
                description = "",
                points = (int?)null,
                startsOn = (string?)null,
                endsOn = (string?)null
            },
            otherTeacher.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCourse_ThatDoesNotExist_ReturnsNotFound()
    {
        var response = await PutAsync(Guid.NewGuid(), new
        {
            title = "Kursen",
            description = "",
            points = (int?)null,
            startsOn = (string?)null,
            endsOn = (string?)null
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCourse_WithTooLongDescription_ReturnsBadRequest()
    {
        var (course, teacher, _) = await AddCourseWithTeacherAndStudentAsync();

        var response = await PutAsync(
            course.Id,
            new
            {
                title = "DevOps",
                description = new string('a', 20_001),
                points = (int?)null,
                startsOn = (string?)null,
                endsOn = (string?)null
            },
            teacher.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("description", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "title is required")]
    [InlineData("   ", "title is required")]
    public async Task UpdateCourse_WithoutTitle_ReturnsBadRequest(string title, string error)
    {
        var (course, teacher, _) = await AddCourseWithTeacherAndStudentAsync();

        var response = await PutAsync(course.Id, new
        {
            title,
            description = "",
            points = (int?)null,
            startsOn = (string?)null,
            endsOn = (string?)null
        }, teacher.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(401)]
    public async Task UpdateCourse_WithPointsOutsideTheRange_ReturnsBadRequest(int points)
    {
        var (course, teacher, _) = await AddCourseWithTeacherAndStudentAsync();

        var response = await PutAsync(course.Id, new
        {
            title = "DevOps",
            description = "",
            points = (int?)points,
            startsOn = (string?)null,
            endsOn = (string?)null
        }, teacher.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("points", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateCourse_WithEndBeforeStart_ReturnsBadRequest()
    {
        var (course, teacher, _) = await AddCourseWithTeacherAndStudentAsync();

        var response = await PutAsync(course.Id, new
        {
            title = "DevOps",
            description = "",
            points = (int?)null,
            startsOn = "2026-11-13",
            endsOn = "2026-10-12"
        }, teacher.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("endsOn", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetStaff_ListsTeachersOfReadingGroupsOnly()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var firstGroup = new Group { SchoolId = school.Id, Name = "Klass A", Term = "HT26" };
        var secondGroup = new Group { SchoolId = school.Id, Name = "Klass B", Term = "HT26" };
        var otherGroup = new Group { SchoolId = school.Id, Name = "Klass C", Term = "HT26" };
        db.Groups.AddRange(firstGroup, secondGroup, otherGroup);

        var course = await TestData.AddCourseAsync(db, school.Id, firstGroup.Id);
        db.GroupCourses.Add(new GroupCourse { GroupId = secondGroup.Id, CourseId = course.Id });
        await TestData.AddCourseAsync(db, school.Id, otherGroup.Id);

        var anna = new User
        {
            SchoolId = school.Id,
            Name = "Anna Andersson",
            Email = "anna@example.com",
            Role = UserRole.Teacher
        };
        var bo = new User
        {
            SchoolId = school.Id,
            Name = "Bo Berg",
            Email = "bo@example.com",
            Role = UserRole.Teacher
        };
        var student = new User
        {
            SchoolId = school.Id,
            Name = "Sara Student",
            Email = "sara@example.com",
            Role = UserRole.Student
        };
        var otherTeacher = new User
        {
            SchoolId = school.Id,
            Name = "Olle Olsson",
            Email = "olle@example.com",
            Role = UserRole.Teacher
        };
        db.Users.AddRange(anna, bo, student, otherTeacher);
        db.Enrollments.AddRange(
            new Enrollment { UserId = anna.Id, GroupId = firstGroup.Id, Role = GroupRole.Teacher },
            new Enrollment { UserId = anna.Id, GroupId = secondGroup.Id, Role = GroupRole.Teacher },
            new Enrollment { UserId = bo.Id, GroupId = secondGroup.Id, Role = GroupRole.Teacher },
            new Enrollment { UserId = student.Id, GroupId = firstGroup.Id, Role = GroupRole.Student },
            new Enrollment { UserId = otherTeacher.Id, GroupId = otherGroup.Id, Role = GroupRole.Teacher });
        await db.SaveChangesAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/courses/{course.Id}/staff");
        request.Headers.Add(TestAuthHandler.Header, student.Id.ToString());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var staff = (await response.Content.ReadFromJsonAsync<List<StaffResponse>>())!;
        Assert.Equal(["Anna Andersson", "Bo Berg"], staff.Select(member => member.Name));
        Assert.Equal(2, staff.Count);
        var annaRow = Assert.Single(staff, member => member.UserId == anna.Id);
        Assert.Equal(["Klass A", "Klass B"], annaRow.Groups);
        Assert.Equal("anna@example.com", annaRow.Email);
        var boRow = Assert.Single(staff, member => member.UserId == bo.Id);
        Assert.Equal(["Klass B"], boRow.Groups);
        Assert.DoesNotContain(staff, member => member.UserId == student.Id);
        Assert.DoesNotContain(staff, member => member.UserId == otherTeacher.Id);
    }

    [Fact]
    public async Task GetStaff_ForCourseThatDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/courses/{Guid.NewGuid()}/staff");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(Course Course, User Teacher, User Student)> AddCourseWithTeacherAndStudentAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Adminen i testerna ser bara sin egen skola, så kursen ligger i den.
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        var student = await TestData.AddMemberAsync(db, group, GroupRole.Student, UserRole.Student);
        return (course, teacher, student);
    }

    private async Task<HttpResponseMessage> PutAsync(Guid courseId, object body, Guid? userId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/courses/{courseId}")
        {
            Content = JsonContent.Create(body)
        };
        if (userId is not null)
        {
            request.Headers.Add(TestAuthHandler.Header, userId.Value.ToString());
        }

        return await _client.SendAsync(request);
    }
}
