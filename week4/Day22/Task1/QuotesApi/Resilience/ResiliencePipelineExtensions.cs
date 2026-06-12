using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Timeout;
using System.Threading.RateLimiting;

namespace QuotesApi.Resilience;

public static class ResiliencePipelineExtensions
{
    public static IServiceCollection AddExternalQuoteClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration["ExternalQuoteService:BaseUrl"] ?? "http://localhost:5001";

        services
            .AddHttpClient<ExternalQuoteClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddResilienceHandler("external-quotes", (pipelineBuilder, ctx) =>
            {
                var logger = ctx.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("QuotesApi.Resilience");

                // ── 1. Bulkhead (Concurrency Limiter) — outermost ──────────────
                // RateLimiterStrategyOptions.DefaultRateLimiterOptions wires a
                // ConcurrencyLimiter internally; OnRejected fires when the queue is full.
                pipelineBuilder.AddRateLimiter(new RateLimiterStrategyOptions
                {
                    DefaultRateLimiterOptions = new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 10,
                        QueueLimit  = 5
                    },
                    OnRejected = _ =>
                    {
                        logger.LogWarning("BULKHEAD: request rejected, too many concurrent calls");
                        return ValueTask.CompletedTask;
                    }
                });

                // ── 2. Total Timeout — hard wall-clock ceiling across ALL attempts ──
                // Without this, 3 retries × 3 s + exponential backoff can run ~16 s.
                // Must sit outside Retry so backoff delays count against the budget.
                pipelineBuilder.AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout   = TimeSpan.FromSeconds(15),
                    OnTimeout = _ =>
                    {
                        logger.LogWarning("TOTAL TIMEOUT: overall budget (15s) exceeded, aborting all retries");
                        return ValueTask.CompletedTask;
                    }
                });

                // ── 3. Circuit Breaker ──────────────────────────────────────────
                // Trips when ALL of the last ≥3 calls within 10 s fail.
                // BreakDuration=30 s then transitions to half-open for a probe.
                pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    SamplingDuration  = TimeSpan.FromSeconds(10),
                    MinimumThroughput = 3,
                    FailureRatio      = 1.0,
                    BreakDuration     = TimeSpan.FromSeconds(30),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    OnOpened = _ =>
                    {
                        logger.LogError("CIRCUIT BREAKER: opened — blocking all calls for 30s");
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = _ =>
                    {
                        logger.LogWarning("CIRCUIT BREAKER: half-open — testing recovery");
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation("CIRCUIT BREAKER: closed — normal traffic resuming");
                        return ValueTask.CompletedTask;
                    }
                });

                // ── 4. Retry with exponential back-off ─────────────────────────
                // Placed BEFORE Timeout so each attempt gets a fresh 3 s budget
                // (true per-attempt timeout, not a shared deadline).
                // POST /api/quotes never touches ExternalQuoteClient, so
                // non-idempotent endpoints are naturally excluded.
                pipelineBuilder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 3,
                    BackoffType      = DelayBackoffType.Exponential,
                    Delay            = TimeSpan.FromSeconds(1),   // delays: ~1 s, ~2 s, ~4 s
                    UseJitter        = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    OnRetry = args =>
                    {
                        logger.LogWarning(
                            "RETRY: attempt {Attempt} after {DelayMs}ms — reason: {Outcome}",
                            args.AttemptNumber + 1,
                            (long)args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message
                                ?? args.Outcome.Result?.StatusCode.ToString()
                                ?? "unknown");
                        return ValueTask.CompletedTask;
                    }
                });

                // ── 5. Per-attempt Timeout — innermost ─────────────────────────
                // Each individual attempt through the retry loop gets exactly 3 s.
                pipelineBuilder.AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout   = TimeSpan.FromSeconds(3),
                    OnTimeout = _ =>
                    {
                        logger.LogWarning("TIMEOUT: call exceeded 3s, cancelling");
                        return ValueTask.CompletedTask;
                    }
                });
            });

        return services;
    }
}
