
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;



using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Services;


var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// SERVICES
// ============================================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLogging();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddTransient<IGuidService, GuidService>();

// Register TokenService so endpoints can inject it
builder.Services.AddScoped<ITokenService, TokenService>();

// ── JWT Authentication ────────────────────────────────────────────────────
// This tells ASP.NET Core: "validate incoming Bearer tokens using these rules"
var jwtKey = builder.Configuration["Jwt:Key"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,   // rejects expired tokens → 401
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero, // no grace period on expiry
        };
    });

builder.Services.AddAuthorization();

// ============================================================================
// MIDDLEWARE PIPELINE
// ============================================================================

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = StatusCodes.Status500InternalServerError;

        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var logger  = context.RequestServices.GetRequiredService<ILogger<Program>>();

        logger.LogError(feature?.Error, "Unhandled exception at {Path}", feature?.Path);

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title  = "An error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Type   = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        });
    });
});

// Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    var logger    = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations");
        throw;
    }
}

app.UseSwagger();
app.UseSwaggerUI();

// ORDER MATTERS: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// ENDPOINTS
// ============================================================================

app.MapAuthEndpoints();    // POST /api/auth/register, /login, /refresh
app.MapQuoteEndpoints();
app.MapCollectionEndpoints();

app.Run();