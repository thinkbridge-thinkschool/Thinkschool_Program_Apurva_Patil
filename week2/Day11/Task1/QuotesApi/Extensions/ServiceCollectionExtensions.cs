using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add infrastructure services: DbContext and repositories
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=quotes.db";

        services.AddDbContext<QuotesDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            // Log all SQL to the console so EF Core queries are visible during profiling.
            // This is the SQLite equivalent of SQL Server's SET STATISTICS IO/TIME ON.
            // Each "Executed DbCommand (Nms)" line shows the query and its wall-clock time.
            options.EnableDetailedErrors()
                   .EnableSensitiveDataLogging()
                   .LogTo(Console.WriteLine, LogLevel.Information,
                          Microsoft.EntityFrameworkCore.Diagnostics.DbContextLoggerOptions.UtcTime);
        });

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        return services;
    }
}
