using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
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
        var schoolId = await AddSchoolAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/groups", new { schoolId, type = "class", name = " NET25 ", term = "HT25" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.Equal("NET25", group!.Name);
        Assert.Equal("Class", group.Type);

        var found = await _client.GetFromJsonAsync<GroupResponse>($"/api/groups/{group.Id}");
        Assert.Equal("HT25", found!.Term);
    }

    [Fact]
    public async Task CreateGroup_WithoutTerm_StoresEmptyTerm()
    {
        var schoolId = await AddSchoolAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/groups", new { schoolId, type = "committee", name = "Elevråd" });

        var group = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("", group!.Term);
    }

    [Fact]
    public async Task CreateGroup_WithSameNameAndTerm_ReturnsBadRequest()
    {
        var schoolId = await AddSchoolAsync();
        var body = new { schoolId, type = "class", name = "NET26", term = "HT26" };
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
        var schoolId = await AddSchoolAsync();
        var response = await _client.PostAsJsonAsync("/api/groups", new { schoolId, type = "class", name = "NET25" });
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
