# Day 19 — Azure Service Bus: Topics, Competing Consumers, Idempotency & DLQ

## Part 1 — Publisher

**File:** `Services/ServiceBusPublisher.cs`

```csharp
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System.Text.Json;

namespace QuotesApi.Services;

public class ServiceBusPublisher
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusPublisher> _logger;
    private readonly string _topicName;
    private readonly string _connectionString;

    public ServiceBusPublisher(
        ServiceBusClient client,
        IConfiguration config,
        ILogger<ServiceBusPublisher> logger)
    {
        _client = client;
        _logger = logger;
        _topicName = config["ServiceBus:TopicName"]
            ?? throw new InvalidOperationException("ServiceBus:TopicName is not configured.");
        _connectionString = config["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("ServiceBus:ConnectionString is not configured.");
    }

    public async Task EnsureSubscriptionsAsync(CancellationToken ct = default)
    {
        var adminClient = new ServiceBusAdministrationClient(_connectionString);

        if (!await adminClient.TopicExistsAsync(_topicName, ct))
        {
            await adminClient.CreateTopicAsync(_topicName, ct);
            _logger.LogInformation("Created topic {Topic}", _topicName);
        }

        foreach (var subscriptionName in new[] { "sub-primary", "sub-audit" })
        {
            if (!await adminClient.SubscriptionExistsAsync(_topicName, subscriptionName, ct))
            {
                await adminClient.CreateSubscriptionAsync(
                    new CreateSubscriptionOptions(_topicName, subscriptionName)
                    {
                        MaxDeliveryCount = 3,
                        LockDuration = TimeSpan.FromSeconds(30)
                    }, ct);
                _logger.LogInformation(
                    "Created subscription {Subscription} on {Topic} (MaxDeliveryCount=3)",
                    subscriptionName, _topicName);
            }
        }
    }

    public async Task SendOrderMessageAsync(int orderId, string action = "process", CancellationToken ct = default)
    {
        await using var sender = _client.CreateSender(_topicName);
        var body = JsonSerializer.Serialize(new { orderId, action });
        var message = new ServiceBusMessage(body)
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };
        message.ApplicationProperties["source"] = "Day19Publisher";
        await sender.SendMessageAsync(message, ct);
        _logger.LogInformation(
            "Sent message MessageId={MessageId} orderId={OrderId} action={Action}",
            message.MessageId, orderId, action);
    }

    public async Task SendPoisonMessageAsync(CancellationToken ct = default)
    {
        await using var sender = _client.CreateSender(_topicName);
        var message = new ServiceBusMessage("INVALID_JSON_{{{")
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };
        message.ApplicationProperties["source"] = "Day19Publisher";
        await sender.SendMessageAsync(message, ct);
        _logger.LogWarning(
            "Sent poison message MessageId={MessageId} — will dead-letter after 3 failed deliveries",
            message.MessageId);
    }
}
```

**Key decision:** Config keys read once at construction with `?? throw` — no `!`
null-forgiving operators anywhere. All three methods accept `CancellationToken`
and forward it to every `await` call. `ServiceBusAdministrationClient` is kept
separate from `ServiceBusClient` — the former handles infrastructure (create
topic/subscriptions), the latter handles data (send/receive messages).

---

## Part 2 — Competing Consumer Worker

**File:** `Services/ServiceBusConsumerWorker.cs`

```csharp
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using System.Text.Json;

namespace QuotesApi.Services;

public class ServiceBusConsumerWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusConsumerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _topicName;
    private readonly string _primarySubscription;

    // Each running instance gets a unique 8-char ID so competing consumers
    // are visible in logs: "Worker [a1b2c3d4] received" vs "Worker [e5f6g7h8] received"
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public ServiceBusConsumerWorker(
        ServiceBusClient client,
        IConfiguration config,
        ILogger<ServiceBusConsumerWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _client = client;
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
                AutoCompleteMessages = false  // Complete/Abandon called explicitly
            });

        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation(
            "Worker [{Instance}] started — {Topic}/{Subscription} MaxConcurrentCalls=3",
            _instanceId, _topicName, _primarySubscription);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync();
        _logger.LogInformation("Worker [{Instance}] stopped", _instanceId);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId = args.Message.MessageId;
        var body = args.Message.Body.ToString();

        _logger.LogInformation(
            "Worker [{Instance}] received MessageId={MessageId} DeliveryCount={DeliveryCount}: {Body}",
            _instanceId, messageId, args.Message.DeliveryCount, body);

        try
        {
            // Scoped DbContext per message — worker is singleton, DbContext is scoped
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

            // Part 3: idempotency check — fast path
            if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, args.CancellationToken))
            {
                _logger.LogInformation("Duplicate detected: {MessageId} — skipping", messageId);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            // Part 4: throws JsonException on poison message body
            var order = JsonSerializer.Deserialize<OrderMessage>(body, JsonOptions)
                ?? throw new InvalidOperationException("Message body deserialized to null");

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
            // Shutdown in progress — let lock expire, Service Bus will redeliver
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
            // Abandon so Service Bus retries. After MaxDeliveryCount the broker
            // moves the message to the dead-letter sub-queue automatically.
            _logger.LogError(ex,
                "Worker [{Instance}] failed on MessageId={MessageId} DeliveryCount={DeliveryCount} — abandoning",
                _instanceId, messageId, args.Message.DeliveryCount);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus processor error: Source={ErrorSource} EntityPath={EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private record OrderMessage(int OrderId, string Action);
}
```

**Key decision:** Three ordered catch blocks — `OperationCanceledException`
(clean shutdown, let lock expire naturally), `DbUpdateException` with UNIQUE
constraint filter (race condition: another worker already wrote this MessageId,
safe to complete not abandon), then general `Exception` (abandon so Service Bus
retries and eventually dead-letters). `IServiceScopeFactory` used because
`DbContext` is scoped and this worker is singleton-lifetime.

---

## Part 3 — Idempotency Key Handling

**File:** `Models/ProcessedMessage.cs`

```csharp
namespace QuotesApi.Models;

public class ProcessedMessage
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
```

**Fluent API in `Data/QuotesDbContext.cs`:**

```csharp
modelBuilder.Entity<ProcessedMessage>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.MessageId).IsRequired().HasMaxLength(50);
    entity.Property(e => e.ProcessedAt).IsRequired();
    // Unique index — safety net for race conditions.
    // AnyAsync is the fast path; this makes the second SaveChangesAsync throw
    // DbUpdateException if two workers pass the check simultaneously.
    entity.HasIndex(e => e.MessageId).IsUnique();
});
```

**Migration `Up()` — confirms unique constraint exists at DB level:**

```csharp
migrationBuilder.CreateTable(
    name: "ProcessedMessages",
    columns: table => new
    {
        Id = table.Column<int>(nullable: false)
            .Annotation("Sqlite:Autoincrement", true),
        MessageId = table.Column<string>(maxLength: 50, nullable: false),
        ProcessedAt = table.Column<DateTimeOffset>(nullable: false)
    },
    constraints: table => table.PrimaryKey("PK_ProcessedMessages", x => x.Id));

migrationBuilder.CreateIndex(
    name: "IX_ProcessedMessages_MessageId",
    table: "ProcessedMessages",
    column: "MessageId",
    unique: true);
```

**Key decision:** Two-layer protection. `AnyAsync` check is the fast path for
99% of calls. The DB-level unique index is the safety net for the 1% race
condition where two concurrent workers both pass the `AnyAsync` check before
either writes. The second `SaveChangesAsync` throws `DbUpdateException` which
is caught separately and completed cleanly — not abandoned.

---

## Part 4 — DLQ Proof

**Step 1 — Poison message sent:**

`POST /api/messages/publish-poison` → `200 OK`

The body sent to the topic is the literal string `INVALID_JSON_{{{` —
intentionally malformed JSON.

*(see: day19-proof-3-poison-message-sent.png)*

---

**Step 2 — Consumer logs showing 3 failed delivery attempts:**

The worker picked up the message and threw `JsonException` on deserialization.
`AbandonMessageAsync` was called each time. Because `MaxDeliveryCount = 3` was
set on the subscription at creation time in `EnsureSubscriptionsAsync`, Service
Bus retried exactly 3 times then moved the message to the DLQ automatically.

```
Worker [87c7d38c] failed on MessageId=10d96d27... DeliveryCount=1 — abandoning
Worker [87c7d38c] failed on MessageId=10d96d27... DeliveryCount=2 — abandoning
Worker [87c7d38c] failed on MessageId=10d96d27... DeliveryCount=3 — abandoning
```

*(see: day19-proof-5-consumer-logs-deliverycount-1-2-3.png)*

---

**Step 3 — DLQ contents confirmed:**

`GET /api/messages/dlq` → `200 OK`

The endpoint uses `ServiceBusReceiver` with `SubQueue = SubQueue.DeadLetter`
and `PeekMessagesAsync` so the message stays in the DLQ and is not consumed.

```json
{
  "count": 1,
  "messages": [{
    "messageId": "10d96d27-0b4e-4298-8f2e-528edbdaec1a",
    "body": "INVALID_JSON_{{{",
    "deadLetterReason": "MaxDeliveryCountExceeded",
    "deadLetterErrorDescription": "Message could not be consumed after 3 delivery attempts.",
    "enqueuedAt": "2026-06-09T12:19:11.565+00:00"
  }]
}
```

*(see: day19-proof-4-dlq-maxdeliverycount-exceeded.png)*

---

## Setup

Add to `appsettings.Development.json` (not committed to git):

```json
{
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "TopicName": "orders",
    "PrimarySubscription": "sub-primary"
  }
}
```

Run the local emulator:

```bash
docker compose up
dotnet run
```

If `ConnectionString` is empty the app starts normally with Service Bus
disabled — all `/api/messages/*` endpoints return `503`.

---

