using System.Net;
using System.Net.Http.Json;
using ApiForge.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiForge.ApiTests;

public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();
    [Fact] public async Task Register_is_public_and_returns_compatible_envelope() { var response = await _client.PostAsJsonAsync("/api/auth/register", new { username="test-user", email="test-user@example.com", password="password123", firstname="Test", lastname="User" }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(); Assert.True(body!.Success); Assert.Equal("Bearer", body.Data!.Type); Assert.NotEmpty(body.Data.Token); }
    [Fact] public async Task Protected_route_without_token_returns_envelope_401() { var response = await _client.GetAsync("/api/content-types"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(); Assert.False(body!.Success); }
}
