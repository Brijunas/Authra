using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Authra.Application.Auth.DTOs;
using Authra.Application.Tenants.DTOs;
using Authra.IntegrationTests.Fixtures;
using AwesomeAssertions;

namespace Authra.IntegrationTests.Tenants;

/// <summary>
/// Integration tests for tenant endpoints (/v1/tenants).
/// </summary>
public class TenantEndpointTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public TenantEndpointTests(DatabaseFixture databaseFixture)
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

    // === Tenant CRUD ===

    [Fact]
    public async Task CreateTenant_WithValidData_ReturnsCreated()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("create@example.com", "Password123!");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new CreateTenantRequest("Test Tenant", "test-tenant"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TenantResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Tenant");
        result.Slug.Should().Be("test-tenant");
        result.Id.Should().StartWith("tnt_");
        result.Status.Should().Be("active");
        result.OwnerId.Should().StartWith("mbr_");
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateSlug_ReturnsConflict()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("duplicate@example.com", "Password123!");

        // Create first tenant
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        firstRequest.Content = JsonContent.Create(new CreateTenantRequest("First", "same-slug"));
        await _fixture.Client.SendAsync(firstRequest);

        // Act - Create second tenant with same slug
        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        secondRequest.Content = JsonContent.Create(new CreateTenantRequest("Second", "same-slug"));
        var response = await _fixture.Client.SendAsync(secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateTenant_WithInvalidSlug_ReturnsBadRequest()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await RegisterAndLoginAsync("invalid@example.com", "Password123!");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new CreateTenantRequest("Test", "Invalid Slug!"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTenant_WithValidId_ReturnsTenant()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("gettenant@example.com", "Get Tenant", "get-tenant");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Debug: Print response if not OK
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Request failed with {response.StatusCode}: {errorBody}. TenantId={tenantId}");
        }

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TenantResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenantId);
        result.Name.Should().Be("Get Tenant");
    }

    [Fact]
    public async Task UpdateTenant_WithValidData_ReturnsUpdatedTenant()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("updatetenant@example.com", "Original", "original-slug");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/tenants/{tenantId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new UpdateTenantRequest("Updated Name"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TenantResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Slug.Should().Be("original-slug"); // Slug not changed
    }

    // === Members ===

    [Fact]
    public async Task ListMembers_ReturnsOwnerAsMember()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("listmembers@example.com", "List Members Tenant", "list-members");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/members");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedMemberResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Email.Should().Be("listmembers@example.com");
        result.Items[0].IsOwner.Should().BeTrue();
    }

    [Fact]
    public async Task GetMember_WithValidId_ReturnsMember()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, memberId, accessToken) = await CreateTenantWithMemberIdAsync("getmember@example.com", "Get Member Tenant", "get-member");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/members/{memberId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TenantMemberResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(memberId);
        result.Email.Should().Be("getmember@example.com");
    }

    // === Invites ===

    [Fact]
    public async Task CreateInvite_WithValidEmail_ReturnsCreated()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("inviter@example.com", "Invite Tenant", "invite-tenant");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/members/invite");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new CreateInviteRequest("invitee@example.com"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<InviteResponse>();
        result.Should().NotBeNull();
        result!.Email.Should().Be("invitee@example.com");
        result.Status.Should().Be("pending");
        result.Id.Should().StartWith("inv_");

        // Verify email was sent
        _fixture.EmailSender.SentEmails.Should().ContainSingle(e => e.To == "invitee@example.com");
    }

    [Fact]
    public async Task CreateInvite_WithExistingMember_ReturnsConflict()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("owner@example.com", "Conflict Tenant", "conflict-tenant");

        // Act - Try to invite the owner
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/members/invite");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new CreateInviteRequest("owner@example.com"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListInvites_ReturnsInvites()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("listinvites@example.com", "List Invites Tenant", "list-invites");

        // Create an invite
        using var inviteRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/members/invite");
        inviteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        inviteRequest.Content = JsonContent.Create(new CreateInviteRequest("invited@example.com"));
        await _fixture.Client.SendAsync(inviteRequest);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/invites");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedInviteResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Email.Should().Be("invited@example.com");
    }

    [Fact]
    public async Task CancelInvite_WithValidId_ReturnsNoContent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("cancelinvite@example.com", "Cancel Invite Tenant", "cancel-invite");

        // Create an invite
        using var inviteRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/members/invite");
        inviteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        inviteRequest.Content = JsonContent.Create(new CreateInviteRequest("tocancel@example.com"));
        var inviteResponse = await _fixture.Client.SendAsync(inviteRequest);

        if (!inviteResponse.IsSuccessStatusCode)
        {
            var errorBody = await inviteResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create invite: {inviteResponse.StatusCode} - {errorBody}");
        }

        var invite = await inviteResponse.Content.ReadFromJsonAsync<InviteResponse>();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/tenants/{tenantId}/invites/{invite!.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // === Authorization ===

    [Fact]
    public async Task GetTenant_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var response = await _fixture.Client.GetAsync("/v1/tenants/tnt_00000000000000000000000000000000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // === Helper Methods ===

    private async Task<(string UserId, string AccessToken)> RegisterAndLoginAsync(string email, string password)
    {
        var registerRequest = new RegisterRequest(email, password);
        await _fixture.Client.PostAsJsonAsync("/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (loginResult!.User.Id, loginResult.AccessToken);
    }

    private async Task<(string TenantId, string AccessToken)> CreateTenantAsync(string email, string tenantName, string tenantSlug)
    {
        var (_, userOnlyToken) = await RegisterAndLoginAsync(email, "Password123!");

        // Create tenant with user-only token
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userOnlyToken);
        createRequest.Content = JsonContent.Create(new CreateTenantRequest(tenantName, tenantSlug));
        var createResponse = await _fixture.Client.SendAsync(createRequest);

        if (!createResponse.IsSuccessStatusCode)
        {
            var errorBody = await createResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create tenant: {createResponse.StatusCode} - {errorBody}");
        }

        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        // Re-login to get tenant-scoped token (auto-selects single tenant)
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, "Password123!"));

        if (!loginResponse.IsSuccessStatusCode)
        {
            var errorBody = await loginResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to login with tenant: {loginResponse.StatusCode} - {errorBody}");
        }

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        if (string.IsNullOrEmpty(loginResult?.AccessToken))
        {
            throw new InvalidOperationException("Login response did not contain access token");
        }

        if (loginResult.Tenant == null)
        {
            throw new InvalidOperationException($"Login response did not contain tenant info (user-only token returned). User has memberships? Check if tenant was created correctly.");
        }

        // Debug: Decode the token to check claims
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(loginResult.AccessToken);
        var tidClaim = jwt.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
        if (string.IsNullOrEmpty(tidClaim) || tidClaim == "00000000-0000-0000-0000-000000000000")
        {
            throw new InvalidOperationException($"Token does not contain valid tid claim. tid={tidClaim}, all claims: {string.Join(", ", jwt.Claims.Select(c => $"{c.Type}={c.Value}"))}");
        }

        return (tenant.Id, loginResult.AccessToken);
    }

    private async Task<(string TenantId, string MemberId, string AccessToken)> CreateTenantWithMemberIdAsync(string email, string tenantName, string tenantSlug)
    {
        var (tenantId, accessToken) = await CreateTenantAsync(email, tenantName, tenantSlug);

        // Get member ID from re-login
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, "Password123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (tenantId, loginResult!.Tenant!.MemberId, accessToken);
    }

    // Response DTOs for testing
    private record PagedMemberResponse(IReadOnlyList<TenantMemberResponse> Items, string? NextCursor, bool HasMore);
    private record PagedInviteResponse(IReadOnlyList<InviteResponse> Items, string? NextCursor, bool HasMore);
}
