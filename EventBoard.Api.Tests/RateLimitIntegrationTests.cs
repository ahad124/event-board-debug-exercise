using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace EventBoard.Api.Tests;

/// <summary>
/// Verifies the auth rate limiter returns 429 once the fixed window is exhausted.
/// Uses a factory that sets a low permit limit so the threshold is reached quickly.
/// </summary>
public class RateLimitIntegrationTests : IClassFixture<RateLimitIntegrationTests.LowLimitFactory>
{
    private readonly LowLimitFactory _factory;

    public RateLimitIntegrationTests(LowLimitFactory factory) => _factory = factory;

    public class LowLimitFactory : IntegrationTestFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:AuthPermitLimit", "3"); // overrides the relaxed default
        }
    }

    [Fact]
    public async Task Login_BeyondLimit_Returns429()
    {
        var client = _factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "nobody@example.com", password = "wrong-password" });
            statuses.Add(resp.StatusCode);
        }

        // First 3 are processed (401 for bad creds); once the window is exhausted → 429.
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
