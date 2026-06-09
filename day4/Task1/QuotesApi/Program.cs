using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QuotesApi.Data;
using QuotesApi.Repositories;
using QuotesApi.Extensions;
using QuotesApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================================
// SERVICES CONFIGURATION
// ============================================================================

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLogging();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddTransient<IGuidService, GuidService>();

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
app.UseSwagger();

app.UseSwaggerUI();

app.MapQuoteEndpoints();
app.MapCollectionEndpoints();

app.Run();
