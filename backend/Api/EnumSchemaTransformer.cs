using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Backend.Api;

/// <summary>
/// En enum som bara används som nullbar (GradeLevel?) får annars ett null-värde i sin lista i OpenAPI-dokumentet,
/// och då blir klientens typ fel. Null står redan på de egenskaper som är nullbara, så enumen får bara sina namn.
/// </summary>
public sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var type = context.JsonTypeInfo.Type;
        var enumType = Nullable.GetUnderlyingType(type) ?? type;
        var isWrapper = schema.OneOf is not null && schema.OneOf.Count > 0;
        if (!enumType.IsEnum || isWrapper)
        {
            return Task.CompletedTask;
        }

        schema.Enum = Enum.GetNames(enumType).Select(name => (JsonNode)JsonValue.Create(name)!).ToList();
        return Task.CompletedTask;
    }
}
