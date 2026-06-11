using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System.Text.Json;

namespace QuotesApi.Services;

public class ServiceBusPublisher
{
    private readonly ServiceBusClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<ServiceBusPublisher> _logger;
    private readonly string _topicName;
    private readonly string _connectionString;

    public ServiceBusPublisher(
        ServiceBusClient client,
        IConfiguration config,
        ILogger<ServiceBusPublisher> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
        _topicName = config["ServiceBus:TopicName"]
            ?? throw new InvalidOperationException("ServiceBus:TopicName is not configured.");
        _connectionString = config["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("ServiceBus:ConnectionString is not configured.");
    }

    // Creates the topic and both subscriptions if they don't already exist.
    // sub-primary  → consumed by ServiceBusConsumerWorker (competing consumers)
    // sub-audit    → consumed by a separate audit reader (each subscription gets its own copy)
    // MaxDeliveryCount = 3 so poison messages reach the DLQ quickly during the demo.
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
            else
            {
                _logger.LogInformation(
                    "Subscription {Subscription} already exists on {Topic}",
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

    // Sends a message with intentionally malformed JSON to demonstrate DLQ behaviour.
    // The consumer will throw on deserialization → AbandonMessageAsync is called each time
    // → after MaxDeliveryCount (3) attempts the broker moves it to the dead-letter sub-queue.
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
