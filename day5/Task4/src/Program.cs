using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Context;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
builder.Services.AddOpenApi();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetRequiredSection("Jwt"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey is required")
    .ValidateOnStart();

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();

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

var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:4200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Ensure SQLite database and schema exist on every cold start.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    db.Database.Migrate();
}

app.Use(async (ctx, next) =>
{
    using (LogContext.PushProperty("TraceId", ctx.TraceIdentifier))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();
app.UseCors("Angular");
app.MapOpenApi();
app.MapScalarApiReference();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


