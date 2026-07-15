using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventBoard.Api.Tests;

/// <summary>
/// Reproducible endpoint response-time benchmark. Seeds a large dataset (~500 events,
/// ~4000 bookings, in addition to the base seed) and times the three endpoints the audit
/// targets, writing avg + p95 to perf-artifacts/{PERF_LABEL}.md.
///
/// This is NOT part of the normal test gate — it is tagged [Trait("Category","Benchmark")]
/// and excluded via `--filter "Category!=Benchmark"`. Run it explicitly:
///   PERF_LABEL=before dotnet test --filter "Category=Benchmark"
/// </summary>
[Trait("Category", "Benchmark")]
public class PerfBenchmarks : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    public PerfBenchmarks(IntegrationTestFactory factory) => _factory = factory;

    private const int ExtraEvents = 500;
    private const int ExtraUsers = 10;
    private const int BookingsPerEvent = 8;   // 500 * 8 = 4000 bookings
    private const int Iterations = 30;

    [Fact]
    public async Task Benchmark_KeyEndpoints()
    {
        var firstEventId = SeedLargeDataset();

        var client = _factory.CreateClient();
        var adminToken = await client.LoginAsync(TestApiHelpers.AdminEmail, TestApiHelpers.AdminPassword);

        var results = new List<(string Endpoint, double Avg, double P95)>
        {
            await TimeAsync("GET /api/events (list)", () => client.GetAsync("/api/events")),
            await TimeAsync($"GET /api/events/{firstEventId} (detail)", () => client.GetAsync($"/api/events/{firstEventId}")),
            await TimeAsync("GET /api/reports/stats (admin)", () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "/api/reports/stats");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
                return client.SendAsync(req);
            }),
        };

        WriteReport(results);
        Assert.All(results, r => Assert.True(r.Avg >= 0));
    }

    private int SeedLargeDataset()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Events.Count() > 100)
            return db.Events.OrderBy(e => e.Id).First(e => e.Title.StartsWith("Perf Event")).Id;

        var categoryId = db.Categories.First().Id;

        // Extra users so each event's bookings use distinct (user,event) pairs.
        var extraUsers = new List<User>();
        for (var u = 0; u < ExtraUsers; u++)
        {
            extraUsers.Add(new User
            {
                Id = Guid.NewGuid(),
                UserName = $"perfuser{u}",
                Email = $"perfuser{u}@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Perf123!"),
                Role = "User"
            });
        }
        db.Users.AddRange(extraUsers);
        db.SaveChanges();

        var rnd = new Random(42);
        var statuses = new[] { BookingStatus.Yes, BookingStatus.Maybe, BookingStatus.No };
        var organizerId = extraUsers[0].Id;
        var firstId = 0;

        for (var i = 0; i < ExtraEvents; i++)
        {
            var evt = new Event
            {
                Title = $"Perf Event {i}",
                Description = "Benchmark seed event with a representative description length.",
                Date = DateTime.UtcNow.AddDays(i % 60),
                Location = "Springfield, IL",
                CategoryId = categoryId,
                OrganizerId = organizerId
            };
            db.Events.Add(evt);
            db.SaveChanges();
            if (i == 0) firstId = evt.Id;

            for (var b = 0; b < BookingsPerEvent; b++)
            {
                db.Bookings.Add(new EventBooking
                {
                    EventId = evt.Id,
                    UserId = extraUsers[b % extraUsers.Count].Id,
                    Status = statuses[rnd.Next(statuses.Length)],
                    BookingDate = DateTime.UtcNow.AddDays(-rnd.Next(30))
                });
            }
        }
        db.SaveChanges();
        return firstId;
    }

    private static async Task<(string, double, double)> TimeAsync(string label, Func<Task<HttpResponseMessage>> call)
    {
        // Warm up (JIT, EF model, first-query compilation).
        (await call()).Dispose();

        var samples = new double[Iterations];
        for (var i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            using var resp = await call();
            await resp.Content.ReadAsByteArrayAsync();
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        var avg = samples.Average();
        var p95 = samples[(int)Math.Ceiling(0.95 * samples.Length) - 1];
        return (label, avg, p95);
    }

    private static void WriteReport(List<(string Endpoint, double Avg, double P95)> results)
    {
        var label = Environment.GetEnvironmentVariable("PERF_LABEL") ?? "before";
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "perf-artifacts");
        Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine($"# Performance benchmark — {label}");
        sb.AppendLine();
        sb.AppendLine($"- Dataset: {ExtraEvents} events, ~{ExtraEvents * BookingsPerEvent} bookings (InMemory EF).");
        sb.AppendLine($"- Iterations per endpoint: {Iterations} (p95 reported; warm-up excluded).");
        sb.AppendLine($"- Captured: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("| Endpoint | Avg (ms) | p95 (ms) |");
        sb.AppendLine("|----------|---------:|---------:|");
        foreach (var (endpoint, avg, p95) in results)
            sb.AppendLine($"| {endpoint} | {avg:F2} | {p95:F2} |");
        sb.AppendLine();

        File.WriteAllText(Path.Combine(dir, $"{label}.md"), sb.ToString());
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EventBoard.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
