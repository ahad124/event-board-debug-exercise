using System.Text;
using System.Text.Json.Serialization;
using EventBoard.Api.Data;
using EventBoard.Api.Repositories;
using EventBoard.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// SECURITY: don't advertise the server implementation.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Structured logging with Serilog: JSON to the console, enriched with request context.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

// Add services to the container
// Serialize/accept enums as their string names (e.g. BookingStatus "Confirmed")
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Add DbContext (SQL Server; connection string comes from configuration/env)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IEventService, EventService>();

// Add Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Weather service: typed HttpClient with a Polly retry policy.
// Retries transient failures 3 times, waiting 2 seconds between each attempt.
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    var baseUrl = builder.Configuration["OpenWeather:BaseUrl"] ?? "https://api.openweathermap.org";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError() // 5xx, 408, and HttpRequestException
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: _ => TimeSpan.FromSeconds(2)));

// SECURITY: fail fast if the JWT signing key is missing or too weak outside Development,
// so a misconfigured deployment cannot start with an insecure/absent secret. Provide the
// key via environment variables or a secret store — never rely on a committed default.
var jwtKey = builder.Configuration["Jwt:Key"];
if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32))
{
    throw new InvalidOperationException(
        "Jwt:Key must be configured with at least 32 bytes. Set it via environment or a secret store.");
}

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// SECURITY: rate limit the authentication endpoints to slow brute-force / credential
// stuffing. Fixed window: 5 requests per 30s per client IP on the "auth" policy.
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 5);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(30),
                QueueLimit = 0
            }));
});

// Health checks: liveness + a database readiness probe.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS. Allowed origins are configurable via "Cors:AllowedOrigins"
// (e.g. Cors__AllowedOrigins__0=http://localhost). When none are configured we
// fall back to AllowAnyOrigin for local development convenience.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // SECURITY: never fall back to AllowAnyOrigin. Default to known local dev
            // origins only; configure Cors:AllowedOrigins for real deployments.
            policy.WithOrigins("http://localhost", "http://localhost:5173")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Per-request structured log line (method, path, status, elapsed ms).
app.UseSerilogRequestLogging();

// SECURITY: baseline security response headers on every response.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    headers["Content-Security-Policy"] = "frame-ancestors 'none'";
    await next();
});

// Configure the HTTP request pipeline.
// SECURITY: Swagger exposes the full API surface, so it is only served in Development
// (or when explicitly enabled via the "EnableSwagger" flag), never in Production by default.
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("EnableSwagger", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS redirection is opt-in. It is disabled by default so the app works over
// plain HTTP inside Docker (behind Nginx) and in local dev without a cert.
if (builder.Configuration.GetValue("UseHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

// Serve uploaded event images from wwwroot/uploads
app.UseStaticFiles();

app.UseCors("AllowFrontend");

// Rate limiting (applies the named "auth" policy where controllers opt in).
app.UseRateLimiter();

// Authentication & Authorization middleware (order matters!)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health endpoints: /health (full, includes DB readiness) and /health/live (liveness only).
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthResponse });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Apply migrations & seed database on startup.
// SQL Server in Docker can take a while to accept connections, so retry a few times.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    const int maxAttempts = 12;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            DbInitializer.Seed(db);
            logger.LogInformation("Database migrated and seeded successfully.");
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex,
                "Database not ready (attempt {Attempt}/{Max}). Retrying in 5s...",
                attempt, maxAttempts);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}

app.Run();

// Writes a compact JSON health payload (overall status + per-check status + duration).
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        durationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description
        })
    });
    return context.Response.WriteAsync(payload);
}

public partial class Program { }