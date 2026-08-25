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
if (builder.Configuration["Storage:Provider"]?.Equals("Postgres", StringComparison.OrdinalIgnoreCase) == true)
{
    builder.Services.AddSingleton<IUserStore, PostgresUserStore>();
    builder.Services.AddSingleton<IPermissionStore, PostgresPermissionStore>();
    builder.Services.AddSingleton<IMediaStore, PostgresMediaStore>();
}
else
{
    builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();
    builder.Services.AddSingleton<IPermissionStore, InMemoryPermissionStore>();
    builder.Services.AddSingleton<IMediaStore, MediaStore>();
}
builder.Services.AddSingleton<JwtTokenService>();
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
    catch (Exception ex)
    {
        var state = ex.GetType().GetProperty("SqlState")?.GetValue(ex)?.ToString();
        context.Response.StatusCode = state == "23505" ? 409 : 500;
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(state == "23505" ? "Resource already exists" : "Internal server error: " + ex.Message));
    }
});

app.MapPost("/api/auth/register", async (RegisterRequest request, IUserStore users, JwtTokenService jwt, CancellationToken ct) =>
{
    if (await users.Find(request.Username, ct) is not null || await users.Find(request.Email, ct) is not null) return Results.Conflict(ApiResponse<object>.Fail("Username or email already exists"));
    var user = await users.Add(request.Username, request.Email, request.Password, request.Firstname, request.Lastname, ["REGISTERED"], ct);
    return Results.Ok(ApiResponse<AuthResponse>.Ok(Auth(user, jwt), "User registered successfully"));
});
app.MapPost("/api/auth/login", async (LoginRequest request, IUserStore users, JwtTokenService jwt, CancellationToken ct) =>
{
    var identifier = string.IsNullOrWhiteSpace(request.Username) ? request.Email : request.Username; var user = identifier is null ? null : await users.Find(identifier, ct);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password)) return Results.Unauthorized();
    if (!user.Enabled) return Results.StatusCode(403);
    return Results.Ok(ApiResponse<AuthResponse>.Ok(Auth(user, jwt), "Login successful"));
});
app.MapPost("/api/auth/validate", (JsonElement request, JwtTokenService jwt) => ApiResponse<bool>.Ok(request.TryGetProperty("token", out var t) && jwt.Validate(t.GetString())));
app.MapPost("/api/auth/refresh", async (RefreshRequest request, IUserStore users, JwtTokenService jwt, CancellationToken ct) =>
{
    var id = jwt.UserId(request.RefreshToken, true); var user = id is null ? null : await users.ById(id.Value, ct); if (user is null || !jwt.Validate(request.RefreshToken, true)) return Results.Unauthorized(); return Results.Ok(ApiResponse<AuthResponse>.Ok(Auth(user, jwt), "Token refreshed"));
});
app.MapGet("/api/auth/oauth2/google", () => Results.Redirect("/api/auth/oauth2/authorize/google"));
app.MapGet("/api/auth/users", async (IUserStore users, CancellationToken ct) => ApiResponse<IReadOnlyList<UserDto>>.Ok((await users.All(ct)).Select(ToDto).ToList()));
app.MapGet("/api/auth/users/{id:long}", async (long id, IUserStore users, CancellationToken ct) => await users.ById(id, ct) is { } u ? Results.Ok(ApiResponse<UserDto>.Ok(ToDto(u))) : Results.NotFound(ApiResponse<object>.Fail("User not found")));
app.MapPut("/api/auth/users/{id:long}/roles", async (long id, JsonElement body, IUserStore users, CancellationToken ct) => { if (!body.TryGetProperty("roles", out var roles)) return Results.BadRequest(ApiResponse<object>.Fail("roles is required")); var u = await users.SetRoles(id, roles.EnumerateArray().Select(x => x.GetString()!).Where(x => x is not null).ToList(), ct); return u is null ? Results.NotFound(ApiResponse<object>.Fail("User not found")) : Results.Ok(ApiResponse<UserDto>.Ok(ToDto(u), "Roles assigned successfully")); });
app.MapDelete("/api/auth/users/{id:long}", async (long id, IUserStore users, CancellationToken ct) => await users.Remove(id, ct) ? Results.Ok(ApiResponse<object?>.Ok(null, "User deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("User not found")));

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

app.MapPost("/api/upload", async (HttpRequest request, IMediaStore media, CancellationToken ct) => { var form = await request.ReadFormAsync(ct); var file = form.Files.GetFile("files") ?? throw new ApiForgeException("files is required", 400); return Results.Ok(ApiResponse<MediaRecord>.Ok(await media.Save(file, ct), "File uploaded successfully")); });
app.MapGet("/api/upload", async (IMediaStore media, CancellationToken ct) => ApiResponse<IReadOnlyList<MediaRecord>>.Ok(await media.All(ct)));
app.MapGet("/api/upload/{id:long}", async (long id, IMediaStore media, CancellationToken ct) => await media.ById(id, ct) is { } x ? Results.Ok(ApiResponse<MediaRecord>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("Media not found")));
app.MapDelete("/api/upload/{id:long}", async (long id, IMediaStore media, CancellationToken ct) => await media.Remove(id, ct) ? Results.Ok(ApiResponse<object?>.Ok(null, "File deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("Media not found")));
app.MapGet("/api/upload/files/{fileName}", async (string fileName, IMediaStore media, CancellationToken ct) => { var x = await media.ByFile(fileName, ct); return x is null || !File.Exists(x.Path) ? Results.NotFound() : Results.File(x.Path, x.Mime ?? "application/octet-stream", x.Name); });

MapPermissions(app);
app.Run();

static AuthResponse Auth(UserRecord u, JwtTokenService jwt) => new(jwt.Access(u.Id, u.Username, u.Roles), jwt.Refresh(u.Id, u.Username, u.Roles), "Bearer", u.Id, u.Username, u.Email, u.Roles);
static UserDto ToDto(UserRecord u) => new(u.Id, u.Username, u.Email, u.Firstname, u.Lastname, u.Roles, u.Enabled);
static Dictionary<string, object?> ToDictionary(JsonElement json) => json.EnumerateObject().ToDictionary(x => x.Name, x => (object?)JsonElementValue(x.Value), StringComparer.OrdinalIgnoreCase);
static object? JsonElementValue(JsonElement x) => x.ValueKind switch { JsonValueKind.String => x.GetString(), JsonValueKind.Number when x.TryGetInt64(out var i) => i, JsonValueKind.Number => x.GetDouble(), JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, _ => x.GetRawText() };
static void MapPermissions(WebApplication app)
{
    app.MapPost("/api/permissions/api", async (ApiPermissionDto x, IPermissionStore s, CancellationToken ct) => Results.Ok(ApiResponse<ApiPermissionDto>.Ok(await s.Add(x, ct), "API permission created successfully")));
    app.MapGet("/api/permissions/api", async (IPermissionStore s, CancellationToken ct) => ApiResponse<IReadOnlyList<ApiPermissionDto>>.Ok(await s.ApiAll(ct)));
    app.MapGet("/api/permissions/api/content-type/{id}", async (string id, IPermissionStore s, CancellationToken ct) => ApiResponse<IReadOnlyList<ApiPermissionDto>>.Ok((await s.ApiAll(ct)).Where(x => x.ContentTypeApiId == id).ToList()));
    app.MapGet("/api/permissions/api/{id:long}", async (long id, IPermissionStore s, CancellationToken ct) => await s.ApiBy(id, ct) is { } x ? Results.Ok(ApiResponse<ApiPermissionDto>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("API permission not found")));
    app.MapPut("/api/permissions/api/{id:long}", async (long id, ApiPermissionDto x, IPermissionStore s, CancellationToken ct) => await s.Update(x with { Id = id }, ct) is { } y ? Results.Ok(ApiResponse<ApiPermissionDto>.Ok(y, "API permission updated successfully")) : Results.NotFound(ApiResponse<object>.Fail("API permission not found")));
    app.MapDelete("/api/permissions/api/{id:long}", async (long id, IPermissionStore s, CancellationToken ct) => await s.RemoveApi(id, ct) ? Results.Ok(ApiResponse<object?>.Ok(null, "API permission deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("API permission not found")));
    app.MapPost("/api/permissions/api/check", async (PermissionCheck x, IPermissionStore s, CancellationToken ct) => ApiResponse<bool>.Ok((await s.ApiAll(ct)).Any(p => p.ContentTypeApiId == x.ContentTypeApiId && p.Endpoint == x.Endpoint && p.Method == x.Method && (p.AllowedRoles ?? []).Intersect(x.UserRoles ?? []).Any())));
    app.MapPost("/api/permissions/content", async (ContentPermissionDto x, IPermissionStore s, CancellationToken ct) => Results.Ok(ApiResponse<ContentPermissionDto>.Ok(await s.Add(x, ct), "Content permission created successfully")));
    app.MapGet("/api/permissions/content", async (IPermissionStore s, CancellationToken ct) => ApiResponse<IReadOnlyList<ContentPermissionDto>>.Ok(await s.ContentAll(ct)));
    app.MapGet("/api/permissions/content/content-type/{id}", async (string id, IPermissionStore s, CancellationToken ct) => ApiResponse<IReadOnlyList<ContentPermissionDto>>.Ok((await s.ContentAll(ct)).Where(x => x.ContentTypeApiId == id).ToList()));
    app.MapGet("/api/permissions/content/{id:long}", async (long id, IPermissionStore s, CancellationToken ct) => await s.ContentBy(id, ct) is { } x ? Results.Ok(ApiResponse<ContentPermissionDto>.Ok(x)) : Results.NotFound(ApiResponse<object>.Fail("Content permission not found")));
    app.MapPut("/api/permissions/content/{id:long}", async (long id, ContentPermissionDto x, IPermissionStore s, CancellationToken ct) => await s.Update(x with { Id = id }, ct) is { } y ? Results.Ok(ApiResponse<ContentPermissionDto>.Ok(y, "Content permission updated successfully")) : Results.NotFound(ApiResponse<object>.Fail("Content permission not found")));
    app.MapDelete("/api/permissions/content/{id:long}", async (long id, IPermissionStore s, CancellationToken ct) => await s.RemoveContent(id, ct) ? Results.Ok(ApiResponse<object?>.Ok(null, "Content permission deleted successfully")) : Results.NotFound(ApiResponse<object>.Fail("Content permission not found")));
    app.MapPost("/api/permissions/content/check", async (PermissionCheck x, IPermissionStore s, CancellationToken ct) => ApiResponse<bool>.Ok((await s.ContentAll(ct)).Any(p => p.ContentTypeApiId == x.ContentTypeApiId && p.Action == x.Action && (p.AllowedRoles ?? []).Intersect(x.UserRoles ?? []).Any())));
}

public partial class Program { }
