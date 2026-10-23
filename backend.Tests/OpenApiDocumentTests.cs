using System.Text.Json;

namespace backend.Tests;

/// <summary>
/// Frontend genererar sina typer från OpenAPI-dokumentet (#35), så det måste beskriva API:et som det är.
/// </summary>
public class OpenApiDocumentTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateContent_Documents201WithContentDto()
    {
        using var document = await GetDocumentAsync();

        var created = document.RootElement
            .GetProperty("paths").GetProperty("/api/content").GetProperty("post")
            .GetProperty("responses").GetProperty("201");
        var schemaRef = created
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref");

        Assert.Equal("#/components/schemas/ContentDto", schemaRef.GetString());
    }

    [Fact]
    public async Task ContentType_IsAStringEnum()
    {
        using var document = await GetDocumentAsync();

        var values = Schema(document, "ContentType").GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString());

        Assert.Equal(["News", "Theory", "Assignment"], values);
    }

    [Fact]
    public async Task CreateContentDto_RequiresOnlyTheNonNullableFields()
    {
        using var document = await GetDocumentAsync();

        var required = Schema(document, "CreateContentDto").GetProperty("required")
            .EnumerateArray()
            .Select(field => field.GetString());

        Assert.Equal(["type", "title", "bodyMd"], required);
    }

    [Fact]
    public async Task Entities_AreNotInTheDocument()
    {
        using var document = await GetDocumentAsync();

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas")
            .EnumerateObject()
            .Select(schema => schema.Name)
            .ToList();

        string[] entities = ["Group", "Course", "Content", "ContentLink", "Comment", "Attachment", "Submission"];
        Assert.Empty(schemas.Intersect(entities));
    }

    private async Task<JsonDocument> GetDocumentAsync() =>
        JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));

    private static JsonElement Schema(JsonDocument document, string name) =>
        document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(name);
}
