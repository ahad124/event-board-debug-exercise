using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace EventBoard.Api.Tests;

/// <summary>
/// Verifies the security hardening: registration cannot self-assign the Admin role,
/// baseline security headers are present, the health endpoints respond, and an admin can
/// still update an event (guards the AsNoTracking read path used by the update flow).
/// </summary>
public class SecurityIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public SecurityIntegrationTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_RequestingAdminRole_CreatesStandardUser()
    {
        var client = _factory.CreateClient();

        // Attempt to self-register as Admin — the role field must be ignored.
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "Mallory",
            email = "mallory@example.com",
            password = "Mallory123!",
            role = "Admin"
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "mallory@example.com", password = "Mallory123!" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<TestApiHelpers.LoginResult>();

        Assert.Equal("User", body!.Role);
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/events");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task Health_Endpoints_ReturnOk()
    {
        var client = _factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var body = await health.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body);
        Assert.Contains("database", body);
    }

    [Fact]
    public async Task UpdateEvent_AsAdmin_PersistsChanges()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.LoginAsync(TestApiHelpers.AdminEmail, TestApiHelpers.AdminPassword);
        client.UseBearer(adminToken);

        var create = await client.PostAsJsonAsync("/api/events", new
        {
            title = "Original Title",
            description = "before",
            date = DateTime.UtcNow.AddDays(6),
            location = "Remote",
            categoryId = 1
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<EventTitleDto>();

        var update = await client.PutAsJsonAsync($"/api/events/{created!.Id}", new { title = "Updated Title" });
        update.EnsureSuccessStatusCode();

        var fetched = await client.GetFromJsonAsync<EventTitleDto>($"/api/events/{created.Id}");
        Assert.Equal("Updated Title", fetched!.Title);
    }

    private class EventTitleDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}
