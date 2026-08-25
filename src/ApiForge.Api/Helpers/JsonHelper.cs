using System.Text.Json;

namespace ApiForge.Api.Helpers;

public static class JsonHelper
{
    public static Dictionary<string, object?> ToDictionary(JsonElement json) =>
        json.EnumerateObject().ToDictionary(x => x.Name, x => (object?)JsonElementValue(x.Value), StringComparer.OrdinalIgnoreCase);

    public static object? JsonElementValue(JsonElement x) => x.ValueKind switch
    {
        JsonValueKind.String => x.GetString(),
        JsonValueKind.Number when x.TryGetInt64(out var i) => i,
        JsonValueKind.Number => x.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => x.GetRawText()
    };
}
