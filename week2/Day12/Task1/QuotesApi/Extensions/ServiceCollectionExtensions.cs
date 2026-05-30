using Microsoft.EntityFrameworkCore;
using QuotesApi.Commands;
using QuotesApi.Data;
using QuotesApi.Queries;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<QuotesDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories (write side)
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        // CQRS-lite: query service (read side) + command handler (write side)
        services.AddScoped<IQuoteQueryService, QuoteQueryService>();
        services.AddScoped<CreateQuoteCommandHandler>();

        return services;
    }
}
