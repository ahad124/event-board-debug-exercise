using System.Net.Http.Json;
using Xunit;

namespace EventBoard.Api.Tests;

/// <summary>
/// RSVP flow tests: the Yes/Maybe/No tallies exposed on an event must reflect the actual
/// responses (guards against a swapped-count mapping bug), and an RSVP is owned per-user
/// so two users responding to the same event keep independent rows (guards against a
/// repository lookup that ignores the user id).
/// </summary>
public class RsvpFlowIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public RsvpFlowIntegrationTests(IntegrationTestFactory factory) => _factory = factory;

    private async Task<int> CreateEventAsync(HttpClient client, string title)
    {
        var create = await client.PostAsJsonAsync("/api/events", new
        {
            title,
            description = "RSVP test event",
            date = DateTime.UtcNow.AddDays(5),
            location = "Remote",
            categoryId = 1
        });
        create.EnsureSuccessStatusCode();
        var dto = await create.Content.ReadFromJsonAsync<EventReadDto>();
        return dto!.Id;
    }

    [Fact]
    public async Task Rsvp_Yes_ShowsInYesCount()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAndLoginAsync("Dana", "dana@example.com", "Dana123!");
        client.UseBearer(token);

        var eventId = await CreateEventAsync(client, "Yes Count Event");

        var rsvp = await client.PostAsJsonAsync("/api/bookings", new { eventId, status = "Yes" });
        rsvp.EnsureSuccessStatusCode();

        var evt = await client.GetFromJsonAsync<EventReadDto>($"/api/events/{eventId}");

        Assert.Equal(1, evt!.RsvpYesCount);
        Assert.Equal(0, evt.RsvpNoCount);
        Assert.Equal(0, evt.RsvpMaybeCount);
        Assert.Equal(1, evt.RsvpTotalCount);
    }

    [Fact]
    public async Task Rsvp_TwoUsers_KeepIndependentResponses()
    {
        var client = _factory.CreateClient();

        // Erin creates an event and RSVPs "Yes".
        var erin = await client.RegisterAndLoginAsync("Erin", "erin@example.com", "Erin123!");
        client.UseBearer(erin);
        var eventId = await CreateEventAsync(client, "Two User RSVP Event");
        (await client.PostAsJsonAsync("/api/bookings", new { eventId, status = "Yes" })).EnsureSuccessStatusCode();

        // Frank RSVPs "No" to the same event.
        client.ClearAuth();
        var frank = await client.RegisterAndLoginAsync("Frank", "frank@example.com", "Frank123!");
        client.UseBearer(frank);
        (await client.PostAsJsonAsync("/api/bookings", new { eventId, status = "No" })).EnsureSuccessStatusCode();

        // Both responses must be recorded independently: Yes=1, No=1, total=2.
        var evt = await client.GetFromJsonAsync<EventReadDto>($"/api/events/{eventId}");
        Assert.Equal(1, evt!.RsvpYesCount);
        Assert.Equal(1, evt.RsvpNoCount);
        Assert.Equal(2, evt.RsvpTotalCount);

        // Frank's own bookings list must contain exactly his single RSVP.
        var mine = await client.GetFromJsonAsync<List<BookingReadDto>>("/api/bookings/my");
        Assert.Single(mine!);
        Assert.Equal("No", mine![0].Status);
    }

    private class EventReadDto
    {
        public int Id { get; set; }
        public int RsvpYesCount { get; set; }
        public int RsvpMaybeCount { get; set; }
        public int RsvpNoCount { get; set; }
        public int RsvpTotalCount { get; set; }
    }

    private class BookingReadDto
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
