using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Services;

public class RelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RelayService> _logger;
    private readonly string? _connectionString;
    private readonly string _topicName;

    private readonly bool _crashAfterPublish;

    public RelayService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<RelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _connectionString = config["ServiceBus:ConnectionString"];
        _topicName = config["ServiceBus:TopicName"] ?? "orders";
        _crashAfterPublish = config.GetValue<bool>("RelayService:CrashAfterPublish");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RelayService started — polling every 5 s");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RelayService loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("RelayService stopped");
    }

    private async Task RelayPendingAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        await using var client = new ServiceBusClient(_connectionString);
        await using var sender = client.CreateSender(_topicName);

        foreach (var outbox in pending)
        {
            // Record the attempt BEFORE publishing so the counter is durable even if we crash.
            outbox.Attempts++;
            await db.SaveChangesAsync(ct);

            var message = new ServiceBusMessage(outbox.Payload)
            {
                // MessageId = OutboxMessage.Id so the consumer can deduplicate by it
                MessageId = outbox.Id.ToString(),
                ContentType = "application/json"
            };
            message.ApplicationProperties["eventType"] = outbox.EventType;

            await sender.SendMessageAsync(message, ct);
            _logger.LogInformation(
                "Relay published OutboxMessage {Id} EventType={EventType} Attempt={Attempt}",
                outbox.Id, outbox.EventType, outbox.Attempts);

            // *** Deliverable 5 crash test — set RelayService:CrashAfterPublish=true in appsettings ***
            // Environment.Exit simulates a hard process crash (not a caught exception).
            // The Attempts increment above already committed, so on restart the counter advances.
            if (_crashAfterPublish)
            {
                _logger.LogWarning(
                    "SIMULATED CRASH after publishing {Id} (attempt {Attempt}) — process exiting now",
                    outbox.Id, outbox.Attempts);
                Environment.Exit(1);
            }

            outbox.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Relay marked {Id} as processed (attempt {Attempt})",
                outbox.Id, outbox.Attempts);
        }
    }
}
