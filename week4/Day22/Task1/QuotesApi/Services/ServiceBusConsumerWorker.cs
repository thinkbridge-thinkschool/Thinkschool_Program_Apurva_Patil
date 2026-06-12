using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuotesApi.Services;

public class ServiceBusConsumerWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<ServiceBusConsumerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _topicName;
    private readonly string _primarySubscription;

    // Each running instance gets a unique 8-char ID so you can observe competing consumers
    // in the logs: "Worker [a1b2c3d4] received ..." vs "Worker [e5f6g7h8] received ..."
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    //every log line identifies which worker instance handled the message

    public ServiceBusConsumerWorker(
        ServiceBusClient client,
        IConfiguration config,
        ILogger<ServiceBusConsumerWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _client = client;
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _topicName = config["ServiceBus:TopicName"]
            ?? throw new InvalidOperationException("ServiceBus:TopicName is not configured.");
        _primarySubscription = config["ServiceBus:PrimarySubscription"]
            ?? throw new InvalidOperationException("ServiceBus:PrimarySubscription is not configured.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = _client.CreateProcessor(_topicName, _primarySubscription,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 3,
                AutoCompleteMessages = false   // we call Complete/Abandon explicitly
            });

        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation(
            "Worker [{Instance}] started — {Topic}/{Subscription} MaxConcurrentCalls=3",
            _instanceId, _topicName, _primarySubscription);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync();
        _logger.LogInformation("Worker [{Instance}] stopped", _instanceId);
    }

    // -------------------------------------------------------------------------
    // Part 2 + Part 3 — message handler with idempotency check
    // Day 20: MessageId for relay-published messages equals OutboxMessage.Id,
    //         so ProcessedMessages serves as the ProcessedEvents deduplication table.
    // -------------------------------------------------------------------------

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId = args.Message.MessageId;
        var body = args.Message.Body.ToString();
        var eventType = args.Message.ApplicationProperties.TryGetValue("eventType", out var et)
            ? et?.ToString() : null;

        _logger.LogInformation(
            "Worker [{Instance}] received MessageId={MessageId} EventType={EventType} DeliveryCount={DeliveryCount}",
            _instanceId, messageId, eventType ?? "order", args.Message.DeliveryCount);

        try
        {
            // Each message handler call gets its own EF Core scope because
            // DbContext is scoped and this worker is a singleton-lifetime hosted service.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

            // --- Idempotency check (Deliverable 4) ---
            // For relay messages: MessageId == OutboxMessage.Id, so this check prevents
            // double-processing even when the relay publishes the same row twice after a crash.
            if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, args.CancellationToken))
            {
                _logger.LogInformation(
                    "Duplicate detected: EventId={MessageId} already in ProcessedMessages — skipping",
                    messageId);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            if (eventType == "QuoteCreated")
            {
                // Relay-published outbox event — process the QuoteCreated notification.
                var quote = JsonSerializer.Deserialize<QuoteCreatedPayload>(body, JsonOptions)
                    ?? throw new InvalidOperationException("QuoteCreated payload deserialized to null");

                _logger.LogInformation(
                    "Worker [{Instance}] QuoteCreated: QuoteId={QuoteId} Author={Author}",
                    _instanceId, quote.Id, quote.Author);
            }
            else
            {
                // --- Part 4: will throw JsonException on the poison message body ---
                var order = JsonSerializer.Deserialize<OrderMessage>(body, JsonOptions)
                    ?? throw new InvalidOperationException("Message body deserialized to null");

                _logger.LogInformation(
                    "Worker [{Instance}] processing orderId={OrderId} action={Action}",
                    _instanceId, order.OrderId, order.Action);
            }

            // Persist deduplication record after successful processing.
            // MessageId == OutboxMessage.Id for relay events, plain GUID for order events.
            db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                ProcessedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(args.CancellationToken);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress — let the lock expire naturally.
            // Service Bus will redeliver to another worker.
            _logger.LogInformation(
                "Worker [{Instance}] cancelled mid-message {MessageId} — lock will expire",
                _instanceId, messageId);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
        {
            // Race condition — another worker wrote the same MessageId first.
            // Safe to complete, not abandon.
            _logger.LogWarning(
                "Worker [{Instance}] race condition on MessageId={MessageId} — completing",
                _instanceId, messageId);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Worker [{Instance}] failed on MessageId={MessageId} DeliveryCount={DeliveryCount} — abandoning",
                _instanceId, messageId, args.Message.DeliveryCount);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    // -------------------------------------------------------------------------
    // Part 2 — processor-level error handler (network faults, lock expiry, etc.)
    // -------------------------------------------------------------------------

    private Task HandleErrorAsync(ProcessErrorEventArgs args)//covers transport-level errors
    {
        _logger.LogError(args.Exception,
            "Service Bus processor error: Source={ErrorSource} EntityPath={EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private record OrderMessage(int OrderId, string Action);
    private record QuoteCreatedPayload(int Id, string Author, string Text, DateTime CreatedAt);
}
