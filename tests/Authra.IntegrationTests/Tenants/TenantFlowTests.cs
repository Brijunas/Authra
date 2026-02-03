using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Authra.Application.Auth.DTOs;
using Authra.Application.Tenants.DTOs;
using Authra.IntegrationTests.Fixtures;
using AwesomeAssertions;

namespace Authra.IntegrationTests.Tenants;

/// <summary>
/// Integration tests for the full tenant creation and login flow.
/// </summary>
public class TenantFlowTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public TenantFlowTests(DatabaseFixture databaseFixture)
    {
        _fixture = new ApiTestFixture(databaseFixture);
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();
    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task FullTenantFlow_RegisterCreateTenantAndLogin_Works()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var email = "flowtest@example.com";
        var password = "Password123!";

        // Step 1: Register
        var registerRequest = new RegisterRequest(email, password);
        var registerResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        registerResult!.Email.Should().Be(email);

        // Step 2: Login (no tenant yet - should get user-only token)
        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginResult!.AccessToken.Should().NotBeNullOrEmpty();
        loginResult.Tenant.Should().BeNull(); // No tenant yet

        // Step 3: Create tenant with user-only token
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);
        createRequest.Content = JsonContent.Create(new CreateTenantRequest("Flow Test Tenant", "flow-test"));
        var createResponse = await _fixture.Client.SendAsync(createRequest);

        if (!createResponse.IsSuccessStatusCode)
        {
            var errorBody = await createResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Failed to create tenant: {createResponse.StatusCode} - {errorBody}");
        }

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenant = await createResponse.Content.ReadFromJsonAsync<TenantResponse>();
        tenant!.Name.Should().Be("Flow Test Tenant");
        tenant.Id.Should().StartWith("tnt_");

        // Step 4: Re-login WITHOUT specifying tenant (should auto-select single tenant)
        var reloginRequest = new LoginRequest(email, password);
        var reloginResponse = await _fixture.Client.PostAsJsonAsync("/v1/auth/login", reloginRequest);

        if (!reloginResponse.IsSuccessStatusCode)
        {
            var errorBody = await reloginResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Failed to re-login: {reloginResponse.StatusCode} - {errorBody}");
        }

        reloginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reloginResult = await reloginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        reloginResult!.AccessToken.Should().NotBeNullOrEmpty();
        reloginResult.Tenant.Should().NotBeNull();
        reloginResult.Tenant!.Id.Should().Be(tenant.Id);
        reloginResult.Tenant.Roles.Should().Contain("owner");
    }
}
