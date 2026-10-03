using System.Text.Json.Serialization;

namespace ApiForge.Core;

public sealed record MediaRecord(
    long Id,
    string Name,
    string? AlternativeText,
    string? Caption,
    int? Width,
    int? Height,
    string Hash,
    string Ext,
    string? Mime,
    double Size,
    string Url,
    string Provider,
    [property: JsonIgnore] string Path
);
