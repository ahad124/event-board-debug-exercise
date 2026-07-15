using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace EventBoard.Api.Tests;

/// <summary>
/// End-to-end auth pipeline tests: a freshly issued token must be usable (guards against
/// an expired-token bug), and role-based authorization on the admin-only Delete endpoint
/// must hold (guards against a broken-RBAC / privilege-escalation bug).
/// </summary>
public class AuthorizationIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public AuthorizationIntegrationTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_IssuesUsableToken_CanCreateEvent()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("Carol", "carol@example.com", "Carol123!");
        client.UseBearer(token);

        var response = await client.PostAsJsonAsync("/api/events", new
        {
            title = "Token Smoke Test",
            description = "Created with a freshly issued JWT",
            date = DateTime.UtcNow.AddDays(3),
            location = "Remote",
            categoryId = 1
        });

        // A valid, unexpired token must authenticate → 201 Created (never 401).
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsAdmin_Returns204()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.LoginAsync(TestApiHelpers.AdminEmail, TestApiHelpers.AdminPassword);
        client.UseBearer(adminToken);

        // Create an event to delete (admin is a valid organizer).
        var create = await client.PostAsJsonAsync("/api/events", new
        {
            title = "Deletable Event",
            description = "To be removed by admin",
            date = DateTime.UtcNow.AddDays(4),
            location = "Remote",
            categoryId = 1
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<EventIdDto>();

        var delete = await client.DeleteAsync($"/api/events/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsNormalUser_Returns403()
    {
        var client = _factory.CreateClient();
        var userToken = await client.LoginAsync(TestApiHelpers.AliceEmail, TestApiHelpers.AlicePassword);
        client.UseBearer(userToken);

        // Alice is a normal user; the Delete endpoint is Admin-only → 403 Forbidden.
        var response = await client.DeleteAsync("/api/events/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private class EventIdDto
    {
        public int Id { get; set; }
    }
}
