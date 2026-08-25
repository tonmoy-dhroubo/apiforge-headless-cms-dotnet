using System.Text.Json;
using System.Text.Json.Serialization;
using ApiForge.Core;
using ApiForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration["Storage:Provider"]?.Equals("Postgres", StringComparison.OrdinalIgnoreCase) == true)
{
    builder.Services.AddSingleton<IContentTypeStore, PostgresContentTypeStore>();
    builder.Services.AddSingleton<IContentStore, PostgresContentStore>();
}
else
{
    builder.Services.AddSingleton<InMemoryContentTypeStore>();
    builder.Services.AddSingleton<IContentTypeStore>(sp => sp.GetRequiredService<InMemoryContentTypeStore>());
    builder.Services.AddSingleton<IContentStore, InMemoryContentStore>();
}
builder.Services.AddSingleton<InMemoryUserStore>();
builder.Services.AddSingleton<InMemoryPermissionStore>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<MediaStore>();
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never; o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()); });
var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        var path = context.Request.Path.Value ?? "";
        var publicRoute = path is "/api/auth/register" or "/api/auth/login" or "/api/auth/validate" || path.StartsWith("/api/upload/files/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
        if (!publicRoute && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            var jwt = context.RequestServices.GetRequiredService<JwtTokenService>();
            var header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || !jwt.Validate(header[7..])) { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Unauthorized")); return; }
        }
        await next();
    }
    catch (ApiForgeException ex)
    {
        context.Response.StatusCode = ex.Status; await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
    }
});

app.MapPost("/api/auth/register", (RegisterRequest request, InMemoryUserStore users, JwtTokenService jwt) =>
{
    if (users.Find(request.Username) is not null || users.Find(request.Email) is not null) return Results.Conflict(ApiResponse<object>.Fail("Username or email already exists"));
    var user = users.Add(request.Username, request.Email, request.Password, request.Firstname, request.Lastname, ["REGISTERED"]);
    return Results.Ok(ApiResponse<AuthResponse>.Ok(Auth(user, jwt), "User registered successfully"));
});
app.MapPost("/api/auth/login", (LoginRequest request, InMemoryUserStore users, JwtTokenService jwt) =>
{
    var identifier = string.IsNullOrWhiteSpace(request.Username) ? request.Email : request.Username; var user = identifier is null ? null : users.Find(identifier);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password)) return Results.Unauthorized();
    if (!user.Enabled) return Results.StatusCode(403);
    return Results.Ok(ApiResponse<AuthResponse>.Ok(Auth(user, jwt), "Login successful"));
});
app.MapPost("/api/auth/validate", (JsonElement request, JwtTokenService jwt) => ApiResponse<bool>.Ok(request.TryGetProperty("token", out var t) && jwt.Validate(t.GetString())));
app.MapPost("/api/auth/refresh", (RefreshRequest request, InMemoryUserStore users, JwtTokenService jwt) =>
{
    var id = jwt.UserId(request.RefreshToken, true); var user = id is null ? null : users.ById(id.Value); if (user is null || !jwt.Validate(request.RefreshToken, true)) return Results.Unauthorized(); return Results.Ok(ApiResponse<AuthResponse>.Ok(Auth(user, jwt), "Token refreshed"));
});
app.MapGet("/api/auth/oauth2/google", () => Results.Redirect("/api/auth/oauth2/authorize/google"));
app.MapGet("/api/auth/users", (InMemoryUserStore users) => ApiResponse<IReadOnlyList<UserDto>>.Ok(users.All().Select(ToDto).ToList()));
app.MapGet("/api/auth/users/{id:long}", (long id, InMemoryUserStore users) => users.ById(id) is { } u ? Results.Ok(ApiResponse<UserDto>.Ok(ToDto(u))) : Results.NotFound(ApiResponse<object>.Fail("User not found")));
app.MapPut("/api/auth/users/{id:long}/roles", (long id, JsonElement body, InMemoryUserStore users) => { var u = users.ById(id); if (u is null) return Results.NotFound(ApiResponse<object>.Fail("User not found")); if (body.TryGetProperty("roles", out var roles)) { u.Roles.Clear(); u.Roles.AddRange(roles.EnumerateArray().Select(x => x.GetString()!).Where(x => x is not null)); } return Results.Ok(ApiResponse<UserDto>.Ok(ToDto(u), "Roles assigned successfully")); });
app.MapDelete("/api/auth/users/{id:long}", (long id, InMemoryUserStore users) => users.Remove(id) ? Results.Ok(ApiResponse<object?>.Ok(null, "User deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("User not found")));

app.MapPost("/api/content-types", async (ContentTypeDto dto, IContentTypeStore store, CancellationToken ct) => Results.Ok(ApiResponse<ContentTypeDto>.Ok(await store.Create(dto, ct), "Content type created successfully")));
app.MapGet("/api/content-types", async (IContentTypeStore store, CancellationToken ct) => ApiResponse<IReadOnlyList<ContentTypeDto>>.Ok(await store.All(ct)));
app.MapGet("/api/content-types/api-id/{apiId}", async (string apiId, IContentTypeStore store, CancellationToken ct) => await store.ByApiId(apiId, ct) is { } x ? Results.Ok(ApiResponse<ContentTypeDto>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("Content type not found")));
app.MapGet("/api/content-types/{id:long}", async (long id, IContentTypeStore store, CancellationToken ct) => await store.ById(id, ct) is { } x ? Results.Ok(ApiResponse<ContentTypeDto>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("Content type not found")));
app.MapPut("/api/content-types/{id:long}", async (long id, ContentTypeDto dto, IContentTypeStore store, CancellationToken ct) => Results.Ok(ApiResponse<ContentTypeDto>.Ok(await store.Update(id, dto, ct), "Content type updated successfully")));
app.MapDelete("/api/content-types/{id:long}", async (long id, IContentTypeStore store, CancellationToken ct) => { await store.Delete(id, ct); return Results.Ok(ApiResponse<object?>.Ok(null, "Content type deleted successfully")); });

app.MapPost("/api/content/{apiId}", async (string apiId, JsonElement body, IContentStore store, CancellationToken ct) => Results.Ok(ApiResponse<IDictionary<string, object?>>.Ok(await store.Create(apiId, ToDictionary(body), ct), "Content created successfully")));
app.MapGet("/api/content/{apiId}", async (string apiId, IContentStore store, CancellationToken ct) => ApiResponse<IReadOnlyList<IDictionary<string, object?>>>.Ok(await store.All(apiId, ct)));
app.MapPost("/api/content/{apiId}/search", async (string apiId, JsonElement body, IContentStore store, CancellationToken ct) => ApiResponse<IReadOnlyList<IDictionary<string, object?>>>.Ok(await store.Search(apiId, ToDictionary(body), ct)));
app.MapGet("/api/content/{apiId}/{id:long}", async (string apiId, long id, IContentStore store, CancellationToken ct) => await store.ById(apiId, id, ct) is { } x ? Results.Ok(ApiResponse<IDictionary<string, object?>>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("Content not found")));
app.MapPut("/api/content/{apiId}/{id:long}", async (string apiId, long id, JsonElement body, IContentStore store, CancellationToken ct) => await store.Update(apiId, id, ToDictionary(body), ct) is { } x ? Results.Ok(ApiResponse<IDictionary<string, object?>>.Ok(x, "Content updated successfully")) : Results.NotFound(ApiResponse<object>.Fail("Content not found")));
app.MapDelete("/api/content/{apiId}/{id:long}", async (string apiId, long id, IContentStore store, CancellationToken ct) => { await store.Delete(apiId, id, ct); return Results.Ok(ApiResponse<object?>.Ok(null, "Content deleted successfully")); });

app.MapPost("/api/upload", async (HttpRequest request, MediaStore media, CancellationToken ct) => { var form = await request.ReadFormAsync(ct); var file = form.Files.GetFile("files") ?? throw new ApiForgeException("files is required", 400); return Results.Ok(ApiResponse<MediaStore.MediaRecord>.Ok(await media.Save(file, ct), "File uploaded successfully")); });
app.MapGet("/api/upload", (MediaStore media) => ApiResponse<IReadOnlyList<MediaStore.MediaRecord>>.Ok(media.All()));
app.MapGet("/api/upload/{id:long}", (long id, MediaStore media) => media.ById(id) is { } x ? Results.Ok(ApiResponse<MediaStore.MediaRecord>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("Media not found")));
app.MapDelete("/api/upload/{id:long}", (long id, MediaStore media) => media.Remove(id) ? Results.Ok(ApiResponse<object?>.Ok(null, "File deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("Media not found")));
app.MapGet("/api/upload/files/{fileName}", (string fileName, MediaStore media) => { var x = media.ByFile(fileName); return x is null || !File.Exists(x.Path) ? Results.NotFound() : Results.File(x.Path, x.Mime ?? "application/octet-stream", x.Name); });

MapPermissions(app);
app.Run();

static AuthResponse Auth(InMemoryUserStore.UserRecord u, JwtTokenService jwt) => new(jwt.Access(u.Id, u.Username, u.Roles), jwt.Refresh(u.Id, u.Username, u.Roles), "Bearer", u.Id, u.Username, u.Email, u.Roles);
static UserDto ToDto(InMemoryUserStore.UserRecord u) => new(u.Id, u.Username, u.Email, u.Firstname, u.Lastname, u.Roles, u.Enabled);
static Dictionary<string, object?> ToDictionary(JsonElement json) => json.EnumerateObject().ToDictionary(x => x.Name, x => (object?)JsonElementValue(x.Value), StringComparer.OrdinalIgnoreCase);
static object? JsonElementValue(JsonElement x) => x.ValueKind switch { JsonValueKind.String => x.GetString(), JsonValueKind.Number when x.TryGetInt64(out var i) => i, JsonValueKind.Number => x.GetDouble(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, _ => x.GetRawText() };
static void MapPermissions(WebApplication app)
{
    app.MapPost("/api/permissions/api", (ApiPermissionDto x, InMemoryPermissionStore s) => Results.Ok(ApiResponse<ApiPermissionDto>.Ok(s.Add(x), "API permission created successfully")));
    app.MapGet("/api/permissions/api", (InMemoryPermissionStore s) => ApiResponse<IEnumerable<ApiPermissionDto>>.Ok(s.Api));
    app.MapGet("/api/permissions/api/content-type/{id}", (string id, InMemoryPermissionStore s) => ApiResponse<IEnumerable<ApiPermissionDto>>.Ok(s.Api.Where(x => x.ContentTypeApiId == id)));
    app.MapGet("/api/permissions/api/{id:long}", (long id, InMemoryPermissionStore s) => s.ApiBy(id) is { } x ? Results.Ok(ApiResponse<ApiPermissionDto>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("API permission not found")));
    app.MapPut("/api/permissions/api/{id:long}", (long id, ApiPermissionDto x, InMemoryPermissionStore s) => s.Update(x with { Id = id }) is { } y ? Results.Ok(ApiResponse<ApiPermissionDto>.Ok(y, "API permission updated successfully")) : Results.NotFound(ApiResponse<object>.Fail("API permission not found")));
    app.MapDelete("/api/permissions/api/{id:long}", (long id, InMemoryPermissionStore s) => s.RemoveApi(id) ? Results.Ok(ApiResponse<object?>.Ok(null, "API permission deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("API permission not found")));
    app.MapPost("/api/permissions/api/check", (PermissionCheck x, InMemoryPermissionStore s) => ApiResponse<bool>.Ok(s.Api.Any(p => p.ContentTypeApiId == x.ContentTypeApiId && p.Endpoint == x.Endpoint && p.Method == x.Method && (p.AllowedRoles ?? []).Intersect(x.UserRoles ?? []).Any())));
    app.MapPost("/api/permissions/content", (ContentPermissionDto x, InMemoryPermissionStore s) => Results.Ok(ApiResponse<ContentPermissionDto>.Ok(s.Add(x), "Content permission created successfully")));
    app.MapGet("/api/permissions/content", (InMemoryPermissionStore s) => ApiResponse<IEnumerable<ContentPermissionDto>>.Ok(s.Content));
    app.MapGet("/api/permissions/content/content-type/{id}", (string id, InMemoryPermissionStore s) => ApiResponse<IEnumerable<ContentPermissionDto>>.Ok(s.Content.Where(x => x.ContentTypeApiId == id)));
    app.MapGet("/api/permissions/content/{id:long}", (long id, InMemoryPermissionStore s) => s.ContentBy(id) is { } x ? Results.Ok(ApiResponse<ContentPermissionDto>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("Content permission not found")));
    app.MapPut("/api/permissions/content/{id:long}", (long id, ContentPermissionDto x, InMemoryPermissionStore s) => s.Update(x with { Id = id }) is { } y ? Results.Ok(ApiResponse<ContentPermissionDto>.Ok(y, "Content permission updated successfully")) : Results.NotFound(ApiResponse<object>.Fail("Content permission not found")));
    app.MapDelete("/api/permissions/content/{id:long}", (long id, InMemoryPermissionStore s) => s.RemoveContent(id) ? Results.Ok(ApiResponse<object?>.Ok(null, "Content permission deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("Content permission not found")));
    app.MapPost("/api/permissions/content/check", (PermissionCheck x, InMemoryPermissionStore s) => ApiResponse<bool>.Ok(s.Content.Any(p => p.ContentTypeApiId == x.ContentTypeApiId && p.Action == x.Action && (p.AllowedRoles ?? []).Intersect(x.UserRoles ?? []).Any())));
}

public partial class Program { }
