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

        // CQRS-lite: both query services registered as keyed services so the benchmark
        // endpoint can resolve each by name; unkeyed default stays EF for existing endpoints.
        services.AddKeyedScoped<IQuoteQueryService, QuoteQueryService>("ef");
        services.AddKeyedScoped<IQuoteQueryService, DapperQuoteQueryService>("dapper");
        services.AddScoped<IQuoteQueryService, QuoteQueryService>();

        // Write side
        services.AddScoped<CreateQuoteCommandHandler>();

        return services;
    }
}
