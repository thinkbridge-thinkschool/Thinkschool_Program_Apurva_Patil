using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Polly;
using Serilog;
using Serilog.Context;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetRequiredSection("Jwt"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey is required")
    .ValidateOnStart();

builder.Services.AddHttpClient("my-service")
    .AddResilienceHandler("default", resilience =>
    {
        resilience.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = args =>
            {
                Log.Warning(
                    "HTTP retry {Attempt} for {OperationKey}. Waiting {DelayMs}ms before next attempt.",
                    args.AttemptNumber,
                    args.Context.OperationKey,
                    args.RetryDelay.TotalMilliseconds);
                return ValueTask.CompletedTask;
            }
        });

        resilience.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(30)
        });

        resilience.AddTimeout(TimeSpan.FromSeconds(10));
    });

builder.Services
    .AddOptions<JwtBearerOptions>("Entra")
    .Configure<IHttpClientFactory>((options, httpClientFactory) =>
    {
        options.Backchannel = httpClientFactory.CreateClient("my-service");
    });

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();

    // Graceful fallback: if no AI connection string is configured,
    // keep local console logging only.
    if (!string.IsNullOrWhiteSpace(aiConnectionString))
    {
        var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
        telemetryConfiguration.ConnectionString = aiConnectionString;

        loggerConfiguration.WriteTo.ApplicationInsights(
            telemetryConfiguration,
            TelemetryConverter.Traces);
    }
});

var jwtOptions = builder.Configuration.GetRequiredSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section is required in configuration");

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
    throw new InvalidOperationException("Jwt:SigningKey is required in configuration");

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "smart";
})
.AddPolicyScheme("smart", "Smart Scheme", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);

            if (jwt.Issuer.Contains("login.microsoftonline.com") ||
                jwt.Issuer.Contains("sts.windows.net"))
                return "Entra";
        }

        return "InternalJwt";
    };
})
.AddJwtBearer("InternalJwt", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer    = jwtOptions.Issuer,
        ValidAudience  = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
    };
})
.AddJwtBearer("Entra", options =>
{
    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var clientId = builder.Configuration["AzureAd:ClientId"];

    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidAudience = clientId,
        ValidIssuer   = $"https://sts.windows.net/{tenantId}/"
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", p =>
        p.RequireClaim("scope", "quotes.write"));

    options.AddPolicy("can-delete-own-quote", p =>
        p.AddRequirements(new QuoteOwnerRequirement()));
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthorizationHandler, QuoteOwnerHandler>();
builder.Services.AddControllers();

// ── OpenTelemetry ────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: "QuotesApi",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("QuotesApi")
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(
                builder.Configuration["OpenTelemetry:OtlpEndpoint"]
                    ?? "http://localhost:4317");
            o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        }));

if (!string.IsNullOrWhiteSpace(aiConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
    {
        options.ConnectionString = aiConnectionString;
    });
}

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=quotes.db"));

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    using (LogContext.PushProperty("TraceId", ctx.TraceIdentifier))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Required so WebApplicationFactory<Program> in the test project can reference this type.
public partial class Program { }
