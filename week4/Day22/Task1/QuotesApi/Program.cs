using System.Text;
using System.Threading.Channels;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Repositories;
using QuotesApi.Resilience;
using QuotesApi.Services;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential(),
        new KeyVaultSecretManager());
}
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================================
// SERVICES CONFIGURATION
// ============================================================================

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLogging();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddTransient<IGuidService, GuidService>();

var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<ITokenService, TokenService>();

// Shared in-memory queue — singleton so both the endpoint and the background service use the same instance
builder.Services.AddSingleton(Channel.CreateBounded<int>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait
}));
builder.Services.AddHostedService<QuoteNotificationService>();

// ============================================================================
// SERVICE BUS — registered only when connection string is present.
// For local development: put the connection string in appsettings.Development.json.
// For production: store it as secret "ServiceBus--ConnectionString" in Key Vault.
// ============================================================================
var serviceBusConnStr = builder.Configuration["ServiceBus:ConnectionString"];
if (!string.IsNullOrWhiteSpace(serviceBusConnStr))
{
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnStr));
    builder.Services.AddSingleton<ServiceBusPublisher>();
    builder.Services.AddHostedService<ServiceBusConsumerWorker>();
}

// Day 20 — Outbox relay: polls OutboxMessages and publishes to Service Bus.
// Registered unconditionally so it can be used even without Service Bus (no-ops when conn string is absent).
builder.Services.AddHostedService<RelayService>();

// Day 22 — Polly v8 resilience pipeline wrapping SlowQuoteService
builder.Services.AddExternalQuoteClient(builder.Configuration);

var app = builder.Build();

// ============================================================================
// STARTUP DIAGNOSTICS
// ============================================================================
var startupLogger = app.Logger;
startupLogger.LogInformation("ASPNETCORE_ENVIRONMENT = {Env}", app.Environment.EnvironmentName);
var resolvedConnStr = app.Configuration["ServiceBus:ConnectionString"];
if (string.IsNullOrWhiteSpace(resolvedConnStr))
    startupLogger.LogWarning("ServiceBus:ConnectionString is EMPTY — Service Bus endpoints will return 503");
else
    startupLogger.LogInformation("ServiceBus:ConnectionString loaded (length={Len})", resolvedConnStr.Length);

// ============================================================================
// MIDDLEWARE PIPELINE
// ============================================================================

// Exception handling middleware
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        logger.LogError(
            exceptionHandlerPathFeature?.Error,
            "Unhandled exception occurred at {Path}",
            exceptionHandlerPathFeature?.Path);

        var problemDetails = new ProblemDetails
        {
            Title = "An error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

// Apply migrations and seed database at startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");

        if (!dbContext.Quotes.Any())
        {
            dbContext.Quotes.AddRange(
                new QuotesApi.Models.Quote { Author = "Aristotle",     Text = "The more you know, the more you realize you don't know." },
                new QuotesApi.Models.Quote { Author = "Marcus Aurelius", Text = "You have power over your mind, not outside events. Realize this, and you will find strength." },
                new QuotesApi.Models.Quote { Author = "Einstein",        Text = "It is not that I'm so smart. But I stay with the questions much longer." },
                new QuotesApi.Models.Quote { Author = "Aristotle",     Text = "We are what we repeatedly do. Excellence, then, is not an act, but a habit." },
                new QuotesApi.Models.Quote { Author = "Marcus Aurelius", Text = "Waste no more time arguing about what a good man should be. Be one." }
            );
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seed data inserted");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not connect to SQL Server — skipping migrations. Resilience endpoints will still work.");
    }
}

// Ensure the Service Bus topic and both subscriptions exist before the worker starts consuming.
// Skipped for the local emulator: ServiceBusAdministrationClient uses HTTPS (port 443) which
// the emulator does not expose. Topics are pre-created from Config/servicebus-config.json instead.
var isEmulator = (app.Configuration["ServiceBus:ConnectionString"] ?? "")
    .Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);

if (!isEmulator && app.Services.GetService<ServiceBusPublisher>() is { } publisher)
{
    try
    {
        startupLogger.LogInformation("Ensuring Service Bus topic and subscriptions exist...");
        await publisher.EnsureSubscriptionsAsync(app.Lifetime.ApplicationStopping);
        startupLogger.LogInformation("Service Bus topic and subscriptions ready");
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "EnsureSubscriptionsAsync failed — check connection string and emulator health");
    }
}
else if (isEmulator)
{
    startupLogger.LogInformation("Emulator detected — skipping EnsureSubscriptionsAsync (topics pre-created from servicebus-config.json)");
}

// ============================================================================
// ENDPOINTS
// ============================================================================
app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapAuthEndpoints();
app.MapQuoteEndpoints();
app.MapCollectionEndpoints();
app.MapMessageEndpoints();

// Fix 5: exposes Redis health status — returns 200 Healthy / 200 Degraded / 503 Unhealthy
app.MapHealthChecks("/health");

// Day 22 — Polly v8 resilience test endpoint
// Routes mode= to SlowQuoteService through the full 4-layer pipeline.
// No authorization required — this is a demo/observability endpoint only.
app.MapGet("/resilience-test", async (
    string? mode,
    ExternalQuoteClient client,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var effectiveMode = mode ?? "ok";

    try
    {
        logger.LogInformation("resilience-test: starting call with {Mode}", effectiveMode);
        var result = await client.FetchQuoteAsync(effectiveMode, ct);
        return Results.Ok(new { mode = effectiveMode, result });
    }
    catch (Polly.CircuitBreaker.BrokenCircuitException ex)
    {
        logger.LogError(
            "resilience-test: circuit open — {Mode} rejected immediately: {Message}",
            effectiveMode, ex.Message);
        return Results.Problem(
            "Circuit breaker is open — all calls blocked for 30 s.",
            statusCode: 503);
    }
    catch (Polly.RateLimiting.RateLimiterRejectedException)
    {
        logger.LogWarning("resilience-test: bulkhead full for {Mode}", effectiveMode);
        return Results.Problem(
            "Too many concurrent requests — bulkhead is full.",
            statusCode: 503);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "resilience-test: all retry attempts exhausted for {Mode}",
            effectiveMode);
        return Results.Problem(
            $"External service unavailable after retries: {ex.GetType().Name}",
            statusCode: 503);
    }
})
.WithName("ResilienceTest")
.WithDescription("Drive the Polly v8 pipeline against SlowQuoteService");

app.Run();
