using System.ComponentModel.DataAnnotations;

namespace ApiForge.Core;

public sealed record FieldDto(
    long? Id,
    [Required, StringLength(100)] string Name,
    [Required, RegularExpression(@"^[a-zA-Z_][a-zA-Z0-9_]*$", ErrorMessage = "FieldName must be alphanumeric and start with a letter or underscore")] string FieldName,
    FieldType Type,
    bool? Required,
    bool? Unique,
    string? TargetContentType,
    string? RelationType
);

public sealed record ContentTypeDto(
    long? Id,
    [Required, StringLength(100)] string Name,
    string? PluralName,
    [Required, RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "ApiId can only contain alphanumeric characters, underscores, and hyphens")] string ApiId,
    string? Description,
    IReadOnlyList<FieldDto>? Fields,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);
