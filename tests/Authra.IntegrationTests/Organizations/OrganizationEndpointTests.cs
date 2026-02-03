using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Authra.Application.Auth.DTOs;
using Authra.Application.Organizations.DTOs;
using Authra.Application.Tenants.DTOs;
using Authra.IntegrationTests.Fixtures;
using AwesomeAssertions;

namespace Authra.IntegrationTests.Organizations;

/// <summary>
/// Integration tests for organization endpoints (/v1/tenants/{tenantId}/organizations).
/// </summary>
public class OrganizationEndpointTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public OrganizationEndpointTests(DatabaseFixture databaseFixture)
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

    // === Organization CRUD ===

    [Fact]
    public async Task CreateOrganization_WithValidData_ReturnsCreated()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("createorg@example.com", "Create Org Tenant", "create-org-tenant");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/organizations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new CreateOrganizationRequest("Test Org", "test-org"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Org");
        result.Slug.Should().Be("test-org");
        result.Id.Should().StartWith("org_");
        result.Status.Should().Be("active");
        result.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateOrganization_WithDuplicateSlug_ReturnsConflict()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("duporg@example.com", "Dup Org Tenant", "dup-org-tenant");

        // Create first organization
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/organizations");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        firstRequest.Content = JsonContent.Create(new CreateOrganizationRequest("First Org", "same-slug"));
        await _fixture.Client.SendAsync(firstRequest);

        // Act - Create second organization with same slug
        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/organizations");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        secondRequest.Content = JsonContent.Create(new CreateOrganizationRequest("Second Org", "same-slug"));
        var response = await _fixture.Client.SendAsync(secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetOrganization_WithValidId_ReturnsOrganization()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, orgId, accessToken) = await CreateOrganizationAsync("getorg@example.com", "Get Org Tenant", "get-org-tenant", "Get Org", "get-org");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/organizations/{orgId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(orgId);
        result.Name.Should().Be("Get Org");
    }

    [Fact]
    public async Task ListOrganizations_ReturnsOrganizations()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, _, accessToken) = await CreateOrganizationAsync("listorg@example.com", "List Org Tenant", "list-org-tenant", "List Org", "list-org");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/organizations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedOrganizationResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("List Org");
    }

    [Fact]
    public async Task UpdateOrganization_WithValidData_ReturnsUpdatedOrganization()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, orgId, accessToken) = await CreateOrganizationAsync("updateorg@example.com", "Update Org Tenant", "update-org-tenant", "Original", "original-org");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/tenants/{tenantId}/organizations/{orgId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new UpdateOrganizationRequest("Updated Name"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Slug.Should().Be("original-org"); // Slug not changed
    }

    [Fact]
    public async Task DeleteOrganization_WithValidId_ReturnsNoContent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, orgId, accessToken) = await CreateOrganizationAsync("deleteorg@example.com", "Delete Org Tenant", "delete-org-tenant", "Delete Org", "delete-org");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/tenants/{tenantId}/organizations/{orgId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify organization is deleted (soft delete)
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/organizations/{orgId}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var getResponse = await _fixture.Client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // === Organization Members ===

    [Fact]
    public async Task AddOrganizationMember_WithValidData_ReturnsCreated()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, orgId, memberId, accessToken) = await CreateOrganizationWithMemberIdAsync("addmember@example.com", "Add Member Tenant", "add-member-tenant", "Add Member Org", "add-member-org");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/organizations/{orgId}/members");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new AddOrganizationMemberRequest(memberId));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<OrganizationMemberResponse>();
        result.Should().NotBeNull();
        result!.TenantMemberId.Should().Be(memberId);
        result.Email.Should().Be("addmember@example.com");
    }

    [Fact]
    public async Task ListOrganizationMembers_ReturnsMembers()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, orgId, memberId, accessToken) = await CreateOrganizationWithMemberIdAsync("listmembers@example.com", "List Members Tenant", "list-members-tenant", "List Members Org", "list-members-org");

        // Add member to organization
        using var addRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/organizations/{orgId}/members");
        addRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        addRequest.Content = JsonContent.Create(new AddOrganizationMemberRequest(memberId));
        await _fixture.Client.SendAsync(addRequest);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/organizations/{orgId}/members");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedOrganizationMemberResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Email.Should().Be("listmembers@example.com");
    }

    [Fact]
    public async Task RemoveOrganizationMember_WithValidId_ReturnsNoContent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, orgId, memberId, accessToken) = await CreateOrganizationWithMemberIdAsync("removemember@example.com", "Remove Member Tenant", "remove-member-tenant", "Remove Member Org", "remove-member-org");

        // Add member to organization first
        using var addRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/organizations/{orgId}/members");
        addRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        addRequest.Content = JsonContent.Create(new AddOrganizationMemberRequest(memberId));
        await _fixture.Client.SendAsync(addRequest);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/tenants/{tenantId}/organizations/{orgId}/members/{memberId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // === Helper Methods ===

    private async Task<(string TenantId, string AccessToken)> CreateTenantAsync(string email, string tenantName, string tenantSlug)
    {
        // Register and login
        var registerRequest = new RegisterRequest(email, "Password123!");
        await _fixture.Client.PostAsJsonAsync("/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, "Password123!");
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        var userOnlyToken = loginResult!.AccessToken;

        // Create tenant
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userOnlyToken);
        createRequest.Content = JsonContent.Create(new CreateTenantRequest(tenantName, tenantSlug));
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();

        // Re-login to get tenant-scoped token
        loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, "Password123!"));
        loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (tenant!.Id, loginResult!.AccessToken);
    }

    private async Task<(string TenantId, string OrgId, string AccessToken)> CreateOrganizationAsync(string email, string tenantName, string tenantSlug, string orgName, string orgSlug)
    {
        var (tenantId, accessToken) = await CreateTenantAsync(email, tenantName, tenantSlug);

        // Create organization
        using var createOrgRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/organizations");
        createOrgRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createOrgRequest.Content = JsonContent.Create(new CreateOrganizationRequest(orgName, orgSlug));
        var createOrgResponse = await _fixture.Client.SendAsync(createOrgRequest);
        var org = await createOrgResponse.Content.ReadFromJsonAsync<OrganizationResponse>();

        return (tenantId, org!.Id, accessToken);
    }

    private async Task<(string TenantId, string OrgId, string MemberId, string AccessToken)> CreateOrganizationWithMemberIdAsync(string email, string tenantName, string tenantSlug, string orgName, string orgSlug)
    {
        var (tenantId, orgId, accessToken) = await CreateOrganizationAsync(email, tenantName, tenantSlug, orgName, orgSlug);

        // Get member ID from login
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, "Password123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (tenantId, orgId, loginResult!.Tenant!.MemberId, accessToken);
    }

    // Response DTOs for testing
    private record PagedOrganizationResponse(IReadOnlyList<OrganizationResponse> Items, string? NextCursor, bool HasMore);
    private record PagedOrganizationMemberResponse(IReadOnlyList<OrganizationMemberResponse> Items, string? NextCursor, bool HasMore);
}
