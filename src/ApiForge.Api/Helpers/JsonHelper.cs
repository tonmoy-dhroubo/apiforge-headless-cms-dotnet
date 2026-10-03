using System.Text.Json;

namespace ApiForge.Api.Helpers;

public static class JsonHelper
{
    /// <summary>
    /// Converts a JsonElement object into a case-insensitive dictionary suitable for dynamic database queries.
    /// </summary>
    public static Dictionary<string, object?> ToDictionary(JsonElement json)
    {
        return json.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ExtractValue(property.Value),
                StringComparer.OrdinalIgnoreCase
            );
    }

    /// <summary>
    /// Extracts the primitive CLR value from a JsonElement.
    /// </summary>
    public static object? ExtractValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var longVal) => longVal,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}
