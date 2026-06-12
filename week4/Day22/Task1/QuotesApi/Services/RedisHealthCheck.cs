using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QuotesApi.Services;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache) => _cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // A read is enough — no need to write anything to Redis to verify connectivity.
            await _cache.GetAsync("health:ping", cancellationToken);
            return HealthCheckResult.Healthy("Redis is reachable.");
        }
        catch (Exception ex)
        {
            // Degraded, not Unhealthy: the app still works with L1-only cache.
            return HealthCheckResult.Degraded($"Redis unreachable: {ex.Message}");
        }
    }
}
