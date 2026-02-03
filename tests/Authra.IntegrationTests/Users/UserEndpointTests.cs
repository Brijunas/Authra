using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Authra.Application.Auth.DTOs;
using Authra.Application.Users.DTOs;
using Authra.IntegrationTests.Fixtures;
using AwesomeAssertions;

namespace Authra.IntegrationTests.Users;

/// <summary>
/// Integration tests for user endpoints (/v1/me).
/// </summary>
public class UserEndpointTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public UserEndpointTests(DatabaseFixture databaseFixture)
    {
        _fixture = new ApiTestFixture(databaseFixture);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.ClearAuthentication();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("user@example.com", "Password123!", "testuser");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        result.Should().NotBeNull();
        result!.Email.Should().Be("user@example.com");
        result.Username.Should().Be("testuser");
        result.Id.Should().StartWith("usr_");
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var response = await _fixture.Client.GetAsync("/v1/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUsername_WithValidData_ReturnsUpdatedUser()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("update@example.com", "Password123!");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new UpdateUsernameRequest("newusername"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        result.Should().NotBeNull();
        result!.Username.Should().Be("newusername");
    }

    [Fact]
    public async Task UpdateUsername_WithShortUsername_ReturnsBadRequest()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("short@example.com", "Password123!");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { username = "ab" });
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUserTenants_WithNoTenants_ReturnsEmptyList()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("notenants@example.com", "Password123!");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<UserTenantResponse>>();
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTenants_WithTenant_ReturnsTenantList()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("withtenants@example.com", "Password123!");

        // Create a tenant
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createRequest.Content = JsonContent.Create(new { name = "My Tenant", slug = "my-tenant" });
        await _fixture.Client.SendAsync(createRequest);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<UserTenantResponse>>();
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Name.Should().Be("My Tenant");
        result[0].Slug.Should().Be("my-tenant");
        result[0].IsOwner.Should().BeTrue();
    }

    private async Task<(string UserId, string AccessToken)> RegisterAndLoginAsync(string email, string password, string? username = null)
    {
        // Register
        var registerRequest = new RegisterRequest(email, password, username);
        await _fixture.Client.PostAsJsonAsync("/v1/auth/register", registerRequest);

        // Login
        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (loginResult!.User.Id, loginResult.AccessToken);
    }
}
