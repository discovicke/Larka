using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class GroupEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record GroupResponse(Guid Id, Guid SchoolId, string Type, string Name, string Term);

    private record CourseResponse(Guid Id, string Code);

    private record MemberResponse(Guid UserId, Guid GroupId, string Role);

    [Fact]
    public async Task CreateGroup_ReturnsCreated_AndCanBeFetched()
    {
        var schoolId = PlaceholderIds.SchoolId;

        var response = await _client.PostAsJsonAsync(
            "/api/groups", new { schoolId, type = "class", name = $" NET{Guid.NewGuid():N} "[..14], term = "HT25" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.StartsWith("NET", group!.Name);
        Assert.DoesNotContain(' ', group.Name);
        Assert.Equal("Class", group.Type);

        var found = await _client.GetFromJsonAsync<GroupResponse>($"/api/groups/{group.Id}");
        Assert.Equal("HT25", found!.Term);
    }

    [Fact]
    public async Task CreateGroup_WithoutTerm_StoresEmptyTerm()
    {
        var schoolId = PlaceholderIds.SchoolId;

        var response = await _client.PostAsJsonAsync(
            "/api/groups", new { schoolId, type = "committee", name = $"Elevråd {Guid.NewGuid():N}"[..16] });

        var group = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("", group!.Term);
    }

    [Fact]
    public async Task CreateGroup_WithSameNameAndTerm_ReturnsBadRequest()
    {
        var schoolId = PlaceholderIds.SchoolId;
        var body = new { schoolId, type = "class", name = $"NET{Guid.NewGuid():N}"[..14], term = "HT26" };
        await _client.PostAsJsonAsync("/api/groups", body);

        var response = await _client.PostAsJsonAsync("/api/groups", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already has a group", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateGroup_WithUnknownSchoolOrType_ReturnsBadRequest()
    {
        var unknownSchool = await _client.PostAsJsonAsync(
            "/api/groups", new { schoolId = Guid.NewGuid(), type = "class", name = "X" });
        var unknownType = await _client.PostAsJsonAsync(
            "/api/groups", new { schoolId = await AddSchoolAsync(), type = "klubb", name = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, unknownSchool.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownType.StatusCode);
    }

    [Fact]
    public async Task GetGroup_ThatDoesNotExist_Returns404()
    {
        var response = await _client.GetAsync($"/api/groups/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddCourse_LinksOnce_ThenConflict()
    {
        var group = await CreateGroupAsync();
        var course = await CreateCourseAsync(group.SchoolId);

        var first = await _client.PostAsync($"/api/groups/{group.Id}/courses/{course.Id}", null);
        var second = await _client.PostAsync($"/api/groups/{group.Id}/courses/{course.Id}", null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var courses = await _client.GetFromJsonAsync<List<CourseResponse>>($"/api/groups/{group.Id}/courses");
        Assert.Equal([course.Id], courses!.Select(item => item.Id));
    }

    [Fact]
    public async Task AddCourse_ThatDoesNotExist_Returns404()
    {
        var group = await CreateGroupAsync();

        var response = await _client.PostAsync($"/api/groups/{group.Id}/courses/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_ReturnsCreated_ThenBadRequestForSameUser()
    {
        var group = await CreateGroupAsync();
        var userId = await AddUserAsync(group.SchoolId);

        var first = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members", new { userId, role = "teacher" });
        var second = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members", new { userId });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var member = await first.Content.ReadFromJsonAsync<MemberResponse>();
        Assert.Equal("Teacher", member!.Role);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task AddMember_FromAnotherSchool_ReturnsBadRequest()
    {
        var group = await CreateGroupAsync();
        var userId = await AddUserAsync(await AddSchoolAsync());

        var response = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members", new { userId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("another school", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AddMember_ToGroupThatDoesNotExist_Returns404()
    {
        var userId = await AddUserAsync(await AddSchoolAsync());

        var response = await _client.PostAsJsonAsync($"/api/groups/{Guid.NewGuid()}/members", new { userId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<GroupResponse> CreateGroupAsync()
    {
        var schoolId = PlaceholderIds.SchoolId;
        var name = $"NET{Guid.NewGuid():N}"[..10];
        var response = await _client.PostAsJsonAsync("/api/groups", new { schoolId, type = "class", name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private async Task<CourseResponse> CreateCourseAsync(Guid schoolId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/courses", new { schoolId, title = "DevOps", code = "DEVOPS" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CourseResponse>())!;
    }

    [Fact]
    public async Task ListGroups_ShowsOnlyTheCallersSchool()
    {
        var other = await AddOtherSchoolAsync();
        var own = await CreateGroupAsync();

        var asAdmin = await _client.GetFromJsonAsync<List<GroupResponse>>("/api/groups");
        var asOtherUser = await GetAsync("/api/groups", other.UserId);

        Assert.Contains(asAdmin!, item => item.Id == own.Id);
        Assert.DoesNotContain(asAdmin!, item => item.Id == other.GroupId);
        Assert.All(asAdmin!, item => Assert.Equal(PlaceholderIds.SchoolId, item.SchoolId));
        var groups = await asOtherUser.Content.ReadFromJsonAsync<List<GroupResponse>>();
        Assert.Equal([other.GroupId], groups!.Select(item => item.Id));
    }

    [Fact]
    public async Task CreateGroup_AtAnotherSchool_ReturnsBadRequest_EvenForAdmin()
    {
        var other = await AddOtherSchoolAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var schoolId = (await db.Groups.FindAsync(other.GroupId))!.SchoolId;

        var response = await _client.PostAsJsonAsync(
            "/api/groups", new { schoolId, type = "class", name = "NET25", term = "HT25" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("school does not exist", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetGroup_AtAnotherSchool_Returns404_EvenForAdmin()
    {
        var other = await AddOtherSchoolAsync();
        var own = await CreateGroupAsync();

        var ownResponse = await _client.GetAsync($"/api/groups/{own.Id}");
        var otherResponse = await _client.GetAsync($"/api/groups/{other.GroupId}");
        var otherCourses = await _client.GetAsync($"/api/groups/{other.GroupId}/courses");

        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherCourses.StatusCode);
    }

    [Fact]
    public async Task AddCourse_AtAnotherSchool_Returns404()
    {
        var other = await AddOtherSchoolAsync();
        var own = await CreateGroupAsync();
        using var scope = factory.Services.CreateScope();
        var ownCourse = await TestData.AddCourseAsync(
            scope.ServiceProvider.GetRequiredService<AppDb>(), PlaceholderIds.SchoolId);

        var otherGroup = await _client.PostAsync($"/api/groups/{other.GroupId}/courses/{ownCourse.Id}", null);
        var otherCourse = await _client.PostAsync($"/api/groups/{own.Id}/courses/{other.CourseId}", null);

        Assert.Equal(HttpStatusCode.NotFound, otherGroup.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherCourse.StatusCode);
    }

    [Fact]
    public async Task AddMember_ToGroupAtAnotherSchool_Returns404()
    {
        var other = await AddOtherSchoolAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/groups/{other.GroupId}/members", new { userId = other.UserId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }

    // En annan skola med en användare, en grupp och en kurs.
    private async Task<(Guid UserId, Guid GroupId, Guid CourseId)> AddOtherSchoolAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var user = await TestData.AddUserAsync(db, school.Id);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id);
        return (user.Id, group.Id, course.Id);
    }

    private async Task<Guid> AddSchoolAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddSchoolAsync(db)).Id;
    }

    private async Task<Guid> AddUserAsync(Guid schoolId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddUserAsync(db, schoolId)).Id;
    }
}
