using System.Text;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Repositories;
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200")
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

var app = builder.Build();

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
                new QuotesApi.Models.Quote { Author = "Seneca",         Text = "It is not that I'm so smart. But I stay with the questions much longer." },
                new QuotesApi.Models.Quote { Author = "Aristotle",     Text = "We are what we repeatedly do. Excellence, then, is not an act, but a habit." },
                new QuotesApi.Models.Quote { Author = "Marcus Aurelius", Text = "Waste no more time arguing about what a good man should be. Be one." }
            );
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seed data inserted");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations");
        throw;
    }
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

app.Run();
