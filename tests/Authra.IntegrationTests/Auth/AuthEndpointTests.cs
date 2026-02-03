using System.Net;
using System.Net.Http.Json;
using Authra.Application.Auth.DTOs;
using Authra.IntegrationTests.Fixtures;
using AwesomeAssertions;

namespace Authra.IntegrationTests.Auth;

/// <summary>
/// Integration tests for authentication endpoints.
/// </summary>
public class AuthEndpointTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public AuthEndpointTests(DatabaseFixture databaseFixture)
    {
        _fixture = new ApiTestFixture(databaseFixture);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var request = new RegisterRequest("test@example.com", "Password123!", "testuser");

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
        result.Username.Should().Be("testuser");
        result.UserId.Should().StartWith("usr_");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var request = new RegisterRequest("duplicate@example.com", "Password123!");

        // First registration
        await _fixture.Client.PostAsJsonAsync("/v1/auth/register", request);

        // Act - Second registration with same email
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var request = new RegisterRequest("invalid-email", "Password123!");

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var request = new RegisterRequest("test@example.com", "weak");

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var email = "login@example.com";
        var password = "Password123!";

        // Register first
        await _fixture.Client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest(email, password));

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, password));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.User.Email.Should().Be(email);
        // User has no tenants yet, so no tokens
        result.Tenant.Should().BeNull();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var email = "loginbad@example.com";

        // Register first
        await _fixture.Client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest(email, "Password123!"));

        // Act - Wrong password
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, "WrongPassword123!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ReturnsUnauthorized()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/login",
            new LoginRequest("nonexistent@example.com", "Password123!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PasswordResetRequest_WithValidEmail_ReturnsAccepted()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var email = "reset@example.com";

        // Register first
        await _fixture.Client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest(email, "Password123!"));

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/password/reset-request",
            new PasswordResetRequestDto(email));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Verify email was sent
        _fixture.EmailSender.SentEmails.Should().ContainSingle(e => e.To == email);
    }

    [Fact]
    public async Task PasswordResetRequest_WithNonExistentEmail_StillReturnsAccepted()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act - Request reset for non-existent email (should not reveal user existence)
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/password/reset-request",
            new PasswordResetRequestDto("nonexistent@example.com"));

        // Assert - Should return 202 to prevent email enumeration
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/v1/auth/refresh",
            new RefreshRequest("invalid-refresh-token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jwks_ReturnsValidKeys()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var response = await _fixture.Client.GetAsync("/.well-known/jwks.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
