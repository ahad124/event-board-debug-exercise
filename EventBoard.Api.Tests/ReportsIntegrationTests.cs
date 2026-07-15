using System.Net.Http.Json;
using Xunit;

namespace EventBoard.Api.Tests;

/// <summary>
/// Admin statistics report. Asserts the seeded breakdown so a swapped/incorrect aggregate
/// (e.g. counting "No" responses as "Yes") is caught. This class only reads data, so the
/// seeded counts are stable.
///
/// Seed (see DbInitializer): 3 users, 10 events, 5 categories, 4 bookings
/// (Yes=2, Maybe=1, No=1), 3 favorites.
/// </summary>
public class ReportsIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public ReportsIntegrationTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Stats_AsAdmin_ReturnsCorrectSeedBreakdown()
    {
        var client = _factory.CreateClient();
        var adminToken = await client.LoginAsync(TestApiHelpers.AdminEmail, TestApiHelpers.AdminPassword);
        client.UseBearer(adminToken);

        var stats = await client.GetFromJsonAsync<StatsReadDto>("/api/reports/stats");

        Assert.NotNull(stats);
        Assert.Equal(3, stats!.TotalUsers);
        Assert.Equal(10, stats.TotalEvents);
        Assert.Equal(5, stats.TotalCategories);
        Assert.Equal(4, stats.TotalRsvps);
        Assert.Equal(2, stats.YesRsvps);
        Assert.Equal(1, stats.MaybeRsvps);
        Assert.Equal(1, stats.NoRsvps);
        Assert.Equal(3, stats.TotalFavorites);
    }

    private class StatsReadDto
    {
        public int TotalUsers { get; set; }
        public int TotalEvents { get; set; }
        public int TotalCategories { get; set; }
        public int TotalRsvps { get; set; }
        public int YesRsvps { get; set; }
        public int MaybeRsvps { get; set; }
        public int NoRsvps { get; set; }
        public int TotalFavorites { get; set; }
    }
}
