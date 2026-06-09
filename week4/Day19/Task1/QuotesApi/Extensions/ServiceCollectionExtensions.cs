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
            options.UseSqlite(connectionString));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        return services;
    }
}
