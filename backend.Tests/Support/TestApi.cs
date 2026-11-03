namespace backend.Tests;

/// <summary>
/// Vanliga uppsättningar via API:t, som användaren klienten är inloggad som (lärare i de flesta tester).
/// </summary>
public static class TestApi
{
    public static Task<ModuleResponse> CreateModuleAsync(
        this HttpClient client, Guid courseId, string title = "Vecka 1", object? publishAt = null) =>
        client.PostJsonAsync<ModuleResponse>($"/api/courses/{courseId}/modules", new { title, publishAt });

    public static Task<ContentResponse> CreateContentAsync(this HttpClient client, object content) =>
        client.PostJsonAsync<ContentResponse>("/api/content", content);

    public static Task<LinkResponse> CreateLinkAsync(this HttpClient client, Guid contentId, object link) =>
        client.PostJsonAsync<LinkResponse>($"/api/content/{contentId}/links", link);

    public static async Task<Guid> CreateAssignmentLinkAsync(
        this HttpClient client, string title, object link, string submissionMode = "text")
    {
        var content = await client.CreateContentAsync(
            new { type = "assignment", title, bodyMd = "", submissionModes = new[] { submissionMode } });
        return (await client.CreateLinkAsync(content.Id, link)).Id;
    }

    public static async Task<Guid> CreateTheoryLinkAsync(this HttpClient client, string title, object link)
    {
        var content = await client.CreateContentAsync(new { type = "theory", title, bodyMd = "" });
        return (await client.CreateLinkAsync(content.Id, link)).Id;
    }

    // Fälten i spec läggs över standardvärdena (titel "Labb 1").
    public static async Task<Guid> CreateAssignmentAsync(this HttpClient client, object spec)
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

        return (await client.PostJsonAsync<IdResponse>("/api/content", body)).Id;
    }

    // Skapar en uppgift med textinlämning och länkar den till modulen. Returnerar länkens id.
    public static async Task<Guid> LinkAssignmentAsync(
        this HttpClient client,
        Guid courseId,
        Guid moduleId,
        string title,
        string? publishAt = null,
        string? dueAt = null,
        bool draft = false,
        Guid? groupId = null,
        string gradeScale = "passFail",
        string[]? resubmitDueAt = null)
    {
        var content = await client.PostJsonAsync<IdResponse>("/api/content", new
        {
            type = "assignment",
            title,
            bodyMd = "",
            status = draft
                ? "draft"
                : "published",
            gradeScale,
            submissionModes = new[] { "text" }
        });
        var link = await client.PostJsonAsync<IdResponse>($"/api/content/{content.Id}/links", new
        {
            courseId,
            groupId,
            moduleId,
            publishAt,
            dueAt,
            resubmitDueAt
        });
        return link.Id;
    }

    // Eleven lämnar in en textinlämning som multipart, som formuläret. Returnerar inlämningens id.
    public static async Task<Guid> SubmitAsync(this HttpClient client, Guid studentId, Guid linkId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/links/{linkId}/submissions")
        {
            Content = new MultipartFormDataContent { { new StringContent("Mitt svar"), "bodyMd" } }
        };
        request.Headers.Add(TestAuthHandler.Header, studentId.ToString());
        using var response = await client.SendAsync(request);
        return (await response.ReadJsonAsync<IdResponse>()).Id;
    }

    public static Task GradeAsync(this HttpClient client, Guid submissionId, object body) =>
        client.PostJsonAsync($"/api/submissions/{submissionId}/grade", body);
}
