namespace ApiForge.Core;

public sealed record FieldDto(
    long? Id,
    string Name,
    string FieldName,
    FieldType Type,
    bool? Required,
    bool? Unique,
    string? TargetContentType,
    string? RelationType
);

public sealed record ContentTypeDto(
    long? Id,
    string Name,
    string? PluralName,
    string ApiId,
    string? Description,
    IReadOnlyList<FieldDto>? Fields,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);
