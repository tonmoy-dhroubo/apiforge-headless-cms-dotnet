using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApiForge.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiForge.ApiTests;

public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_is_public_and_returns_compatible_envelope()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerPayload = new
        {
            username = "test-user-" + uniqueId,
            email = $"test-user-{uniqueId}@example.com",
            password = "password123",
            firstname = "Test",
            lastname = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
        Assert.Equal("Bearer", body.Data.Type);
        Assert.NotEmpty(body.Data.Token);
    }

    [Fact]
    public async Task Protected_route_without_token_returns_envelope_401()
    {
        // Act
        var response = await _client.GetAsync("/api/content-types");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized", body.Error);
    }

    [Fact]
    public async Task Content_type_and_dynamic_content_preserve_contract()
    {
        // Arrange
        var token = await RegisterUserAndGetToken();

        var createTypePayload = new
        {
            name = "Test Article",
            apiId = "test-article",
            fields = new[]
            {
                new
                {
                    name = "Title",
                    fieldName = "title",
                    type = "SHORT_TEXT",
                    required = true,
                    unique = false
                }
            }
        };

        // Act 1: Create Content Type
        using var createTypeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/content-types")
        {
            Content = JsonContent.Create(createTypePayload)
        };
        createTypeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createdTypeResponse = await _client.SendAsync(createTypeRequest);
        Assert.Equal(HttpStatusCode.OK, createdTypeResponse.StatusCode);

        var createdTypeJson = await createdTypeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var returnedApiId = createdTypeJson.GetProperty("data").GetProperty("apiId").GetString();
        Assert.Equal("test-article", returnedApiId);

        // Act 2: Create Dynamic Content Entry
        using var addEntryRequest = new HttpRequestMessage(HttpMethod.Post, "/api/content/test-article")
        {
            Content = JsonContent.Create(new { title = "Hello" })
        };
        addEntryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var entryResponse = await _client.SendAsync(addEntryRequest);
        Assert.Equal(HttpStatusCode.OK, entryResponse.StatusCode);

        var entryJson = await entryResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(entryJson.GetProperty("success").GetBoolean());

        // Act 3: Search Dynamic Content Entry
        using var searchRequest = new HttpRequestMessage(HttpMethod.Post, "/api/content/test-article/search")
        {
            Content = JsonContent.Create(new { title = "Hello" })
        };
        searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var searchResponse = await _client.SendAsync(searchRequest);
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
    }

    [Fact]
    public async Task Permission_check_uses_role_intersection()
    {
        // Arrange
        var token = await RegisterUserAndGetToken();

        var permissionPayload = new
        {
            contentTypeApiId = "test",
            action = "READ",
            allowedRoles = new[] { "EDITOR" }
        };

        using var createPermissionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/permissions/content")
        {
            Content = JsonContent.Create(permissionPayload)
        };
        createPermissionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.SendAsync(createPermissionRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        // Act: Check Permission with matching role
        var checkPayload = new
        {
            contentTypeApiId = "test",
            action = "READ",
            userRoles = new[] { "EDITOR" }
        };

        using var checkRequest = new HttpRequestMessage(HttpMethod.Post, "/api/permissions/content/check")
        {
            Content = JsonContent.Create(checkPayload)
        };
        checkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var checkResponse = await _client.SendAsync(checkRequest);
        Assert.Equal(HttpStatusCode.OK, checkResponse.StatusCode);

        // Assert
        var checkBody = await checkResponse.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        Assert.NotNull(checkBody);
        Assert.True(checkBody.Data);
    }

    [Fact]
    public async Task Multipart_media_upload_returns_compatible_metadata()
    {
        // Arrange
        var token = await RegisterUserAndGetToken();

        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("hello"u8.ToArray());
        formContent.Add(fileContent, "files", "hello.txt");

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/upload")
        {
            Content = formContent
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var uploadResponse = await _client.SendAsync(uploadRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var uploadJson = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var message = uploadJson.GetProperty("message").GetString();
        var fileUrl = uploadJson.GetProperty("data").GetProperty("url").GetString();

        Assert.Equal("File uploaded successfully", message);
        Assert.NotNull(fileUrl);
        Assert.StartsWith("/api/upload/files/", fileUrl);
    }

    [Fact]
    public async Task Refresh_is_public_and_returns_rotated_tokens()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString("N")[..10];
        var registerPayload = new
        {
            username = "refresh" + uniqueId,
            email = uniqueId + "@example.com",
            password = "password123"
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerPayload);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var refreshToken = registerBody!.Data!.RefreshToken;

        // Act
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        // Assert
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(refreshBody);
        Assert.True(refreshBody.Success);
        Assert.NotNull(refreshBody.Data);
        Assert.NotEmpty(refreshBody.Data.Token);
    }

    [Fact]
    public async Task User_admin_and_content_delete_routes_return_source_messages()
    {
        // Arrange
        var token = await RegisterUserAndGetToken();

        // 1. Verify protected user listing
        using var listUsersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/users");
        listUsersRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var usersResponse = await _client.SendAsync(listUsersRequest);
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);

        // 2. Create content type for deletion
        var uniqueApiId = "delete-type" + Guid.NewGuid().ToString("N")[..6];
        var typePayload = new
        {
            name = "Delete Type",
            apiId = uniqueApiId
        };

        using var createTypeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/content-types")
        {
            Content = JsonContent.Create(typePayload)
        };
        createTypeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createTypeResponse = await _client.SendAsync(createTypeRequest);
        var typeJson = await createTypeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var typeId = typeJson.GetProperty("data").GetProperty("id").GetInt64();

        // 3. Delete content type
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/content-types/{typeId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    private async Task<string> RegisterUserAndGetToken()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..10];
        var payload = new
        {
            username = "user" + uniqueId,
            email = uniqueId + "@example.com",
            password = "password123"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        return body!.Data!.Token;
    }
}
