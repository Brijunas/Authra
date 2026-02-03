using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Authra.Application.Auth.DTOs;
using Authra.Application.Roles.DTOs;
using Authra.Application.Tenants.DTOs;
using Authra.IntegrationTests.Fixtures;
using AwesomeAssertions;

namespace Authra.IntegrationTests.Roles;

/// <summary>
/// Integration tests for role endpoints (/v1/tenants/{tenantId}/roles).
/// </summary>
public class RoleEndpointTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public RoleEndpointTests(DatabaseFixture databaseFixture)
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

    // === Role CRUD ===

    [Fact]
    public async Task CreateRole_WithValidData_ReturnsCreated()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("createrole@example.com", "Create Role Tenant", "create-role-tenant");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new CreateRoleRequest("editor", "Editor", "Can edit content"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<RoleResponse>();
        result.Should().NotBeNull();
        result!.Code.Should().Be("editor");
        result.Name.Should().Be("Editor");
        result.Description.Should().Be("Can edit content");
        result.Id.Should().StartWith("rol_");
        result.IsSystem.Should().BeFalse();
        result.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRole_WithDuplicateCode_ReturnsConflict()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("duprole@example.com", "Dup Role Tenant", "dup-role-tenant");

        // Create first role
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/roles");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        firstRequest.Content = JsonContent.Create(new CreateRoleRequest("same-code", "First Role"));
        await _fixture.Client.SendAsync(firstRequest);

        // Act - Create second role with same code
        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/roles");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        secondRequest.Content = JsonContent.Create(new CreateRoleRequest("same-code", "Second Role"));
        var response = await _fixture.Client.SendAsync(secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetRole_WithValidId_ReturnsRole()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, roleId, accessToken) = await CreateRoleAsync("getrole@example.com", "Get Role Tenant", "get-role-tenant", "viewer", "Viewer");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/roles/{roleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoleResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(roleId);
        result.Code.Should().Be("viewer");
        result.Name.Should().Be("Viewer");
    }

    [Fact]
    public async Task ListRoles_ReturnsSystemAndCustomRoles()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("listroles@example.com", "List Roles Tenant", "list-roles-tenant");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedRoleResponse>();
        result.Should().NotBeNull();
        // Should have at least owner and member roles from tenant creation
        result!.Items.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Items.Should().Contain(r => r.Code == "owner" && r.IsSystem);
        result.Items.Should().Contain(r => r.Code == "member" && r.IsDefault);
    }

    [Fact]
    public async Task UpdateRole_WithValidData_ReturnsUpdatedRole()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, roleId, accessToken) = await CreateRoleAsync("updaterole@example.com", "Update Role Tenant", "update-role-tenant", "original", "Original");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/tenants/{tenantId}/roles/{roleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new UpdateRoleRequest("Updated Name", "Updated Description"));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoleResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
        result.Code.Should().Be("original"); // Code not changed
    }

    [Fact]
    public async Task DeleteRole_WithValidId_ReturnsNoContent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, roleId, accessToken) = await CreateRoleAsync("deleterole@example.com", "Delete Role Tenant", "delete-role-tenant", "todelete", "To Delete");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/tenants/{tenantId}/roles/{roleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify role is deleted
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/roles/{roleId}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var getResponse = await _fixture.Client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteRole_SystemRole_ReturnsForbidden()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, accessToken) = await CreateTenantAsync("deletesystem@example.com", "Delete System Tenant", "delete-system-tenant");

        // Get owner role ID
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/roles");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var listResponse = await _fixture.Client.SendAsync(listRequest);
        var roles = await listResponse.Content.ReadFromJsonAsync<PagedRoleResponse>();
        var ownerRoleId = roles!.Items.First(r => r.Code == "owner").Id;

        // Act - Try to delete owner role
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/tenants/{tenantId}/roles/{ownerRoleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // === Role Assignments ===

    [Fact]
    public async Task AssignRole_WithValidData_ReturnsCreated()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, roleId, memberId, accessToken) = await CreateRoleWithMemberIdAsync("assignrole@example.com", "Assign Role Tenant", "assign-role-tenant", "newrole", "New Role");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/members/{memberId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new AssignRoleRequest(roleId));
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<MemberRoleResponse>();
        result.Should().NotBeNull();
        result!.RoleId.Should().Be(roleId);
        result.RoleCode.Should().Be("newrole");
    }

    [Fact]
    public async Task ListMemberRoles_ReturnsRoles()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, memberId, accessToken) = await CreateTenantWithMemberIdAsync("listmemberroles@example.com", "List Member Roles Tenant", "list-member-roles-tenant");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/tenants/{tenantId}/members/{memberId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<MemberRoleResponse>>();
        result.Should().NotBeNull();
        // Owner should have owner role
        result!.Should().Contain(r => r.RoleCode == "owner");
    }

    [Fact]
    public async Task UnassignRole_WithValidId_ReturnsNoContent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (tenantId, roleId, memberId, accessToken) = await CreateRoleWithMemberIdAsync("unassignrole@example.com", "Unassign Role Tenant", "unassign-role-tenant", "toremove", "To Remove");

        // Assign role first
        using var assignRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/members/{memberId}/roles");
        assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        assignRequest.Content = JsonContent.Create(new AssignRoleRequest(roleId));
        await _fixture.Client.SendAsync(assignRequest);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/tenants/{tenantId}/members/{memberId}/roles/{roleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // === Permissions ===

    [Fact]
    public async Task ListSystemPermissions_ReturnsPermissions()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var (_, accessToken) = await CreateTenantAsync("listperms@example.com", "List Perms Tenant", "list-perms-tenant");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/permissions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<PermissionResponse>>();
        result.Should().NotBeNull();
        result!.Count.Should().BeGreaterThanOrEqualTo(19); // 19 system permissions
        result.Should().Contain(p => p.Code == "tenant:read");
        result.Should().Contain(p => p.Code == "organizations:create");
        result.Should().Contain(p => p.Code == "roles:assign");
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

    private async Task<(string TenantId, string MemberId, string AccessToken)> CreateTenantWithMemberIdAsync(string email, string tenantName, string tenantSlug)
    {
        var (tenantId, accessToken) = await CreateTenantAsync(email, tenantName, tenantSlug);

        // Get member ID from login
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, "Password123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (tenantId, loginResult!.Tenant!.MemberId, accessToken);
    }

    private async Task<(string TenantId, string RoleId, string AccessToken)> CreateRoleAsync(string email, string tenantName, string tenantSlug, string roleCode, string roleName)
    {
        var (tenantId, accessToken) = await CreateTenantAsync(email, tenantName, tenantSlug);

        // Create role
        using var createRoleRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/roles");
        createRoleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createRoleRequest.Content = JsonContent.Create(new CreateRoleRequest(roleCode, roleName));
        var createRoleResponse = await _fixture.Client.SendAsync(createRoleRequest);
        var role = await createRoleResponse.Content.ReadFromJsonAsync<RoleResponse>();

        return (tenantId, role!.Id, accessToken);
    }

    private async Task<(string TenantId, string RoleId, string MemberId, string AccessToken)> CreateRoleWithMemberIdAsync(string email, string tenantName, string tenantSlug, string roleCode, string roleName)
    {
        var (tenantId, roleId, accessToken) = await CreateRoleAsync(email, tenantName, tenantSlug, roleCode, roleName);

        // Get member ID from login
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(email, "Password123!"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        return (tenantId, roleId, loginResult!.Tenant!.MemberId, accessToken);
    }

    // Response DTOs for testing
    private record PagedRoleResponse(IReadOnlyList<RoleResponse> Items, string? NextCursor, bool HasMore);
}
