using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventBoard.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventBoard.Api.Tests;

/// <summary>
/// Like <see cref="CustomWebApplicationFactory"/> but every factory instance gets its
/// own uniquely-named InMemory database, so integration test classes are fully isolated
/// from one another (no shared global "IntegrationTestDb" state).
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"ITDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Relax auth rate limiting for functional tests (a dedicated test sets it low).
        builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            DbInitializer.Seed(db);
        });
    }
}

/// <summary>Auth/HTTP helpers shared by the integration tests.</summary>
public static class TestApiHelpers
{
    // Seeded accounts (see DbInitializer).
    public const string AdminEmail = "admin@eventboard.com";
    public const string AdminPassword = "Admin123!";
    public const string AliceEmail = "alice@example.com";
    public const string AlicePassword = "Alice123!";

    public record LoginResult(string Token, string Role, string UserName);

    public static async Task<string> RegisterAndLoginAsync(
        this HttpClient client, string userName, string email, string password, string role = "User")
    {
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { userName, email, password, role });
        register.EnsureSuccessStatusCode();
        return await client.LoginAsync(email, password);
    }

    public static async Task<string> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResult>();
        return body!.Token;
    }

    public static void UseBearer(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static void ClearAuth(this HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = null;
}
