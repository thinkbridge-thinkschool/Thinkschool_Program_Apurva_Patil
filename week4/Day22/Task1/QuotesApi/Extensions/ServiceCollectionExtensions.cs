using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuotesApi.Data;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add infrastructure services: DbContext, repositories, and caching.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=quotes.db";

        services.AddDbContext<QuotesDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        // Fix 5: always register the health-check pipeline; /health endpoint is mapped in Program.cs
        var hcBuilder = services.AddHealthChecks();

        // L2: Redis distributed cache — HybridCache picks this up automatically.
        // Falls back to L1-only when Redis is not available.
        var redisConnStr = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnStr))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnStr;
            });
            // Degraded (not Unhealthy) so the app stays up but Redis failure is visible on /health
            hcBuilder.AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded);
        }

        // HybridCache: L1 in-memory + L2 Redis, 30 s TTL.
        // Stampede protection is built-in: only one factory call executes per key
        // regardless of how many concurrent requests arrive simultaneously.
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromSeconds(30),
                LocalCacheExpiration = TimeSpan.FromSeconds(30)
            };
        });

        return services;
    }
}
