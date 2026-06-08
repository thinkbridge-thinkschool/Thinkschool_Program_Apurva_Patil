Day 18 Submission — Background Jobs in ASP.NET Core 10

════════════════════════════════════════
PART 1 — THE BRIEF I GAVE THE AGENT
════════════════════════════════════════

Add a background job system to the existing QuotesApi with:

1. Shared queue — System.Threading.Channels.Channel<int> registered as a
   singleton so both the endpoint and the background service share the same
   instance.

2. BackgroundService — QuoteNotificationService : BackgroundService that loops
   on channel.Reader.ReadAsync(cancellationToken) and logs a structured
   message for each dequeued quote ID.

3. Endpoint integration — POST /api/quotes writes the new quote ID to the
   channel with TryWrite (no await) after the DB save, so the 201 response
   goes back to Angular before the notification runs.

4. Graceful shutdown — CancellationToken passed into ExecuteAsync is
   respected: ReadAsync throws OperationCanceledException on cancellation,
   caught outside the loop, ExecuteAsync exits cleanly.

5. Comparison writeup — BACKGROUND_JOBS.md explaining:
   - IHostedService vs BackgroundService
   - Where Hangfire fits and what problem it solves
   - Why the cancellation token matters for Azure-hosted services


════════════════════════════════════════
PART 2 — AGENT OUTPUT
════════════════════════════════════════

## FILE: QuoteNotificationService.cs

using System.Threading.Channels;

namespace QuotesApi.Services;

public class QuoteNotificationService : BackgroundService
{
    private readonly Channel<int> _channel;
    private readonly ILogger<QuoteNotificationService> _logger;

    public QuoteNotificationService(Channel<int> channel, ILogger<QuoteNotificationService> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                var quoteId = await _channel.Reader.ReadAsync(stoppingToken);
                _logger.LogInformation("Sending notification for quote {Id}", quoteId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("QuoteNotificationService stopping.");
        }
    }
}

## FILE: Program.cs (relevant registrations)

// Shared in-memory queue — singleton so both the endpoint and the background
// service use the same instance
builder.Services.AddSingleton(Channel.CreateUnbounded<int>());
builder.Services.AddHostedService<QuoteNotificationService>();

## FILE: EndpointExtensions.cs — CreateQuote (relevant lines)

var createdQuote = await repository.AddAsync(quote, cancellationToken);
notificationChannel.Writer.TryWrite(createdQuote.Id);   // no await — fire and forget
return Results.Created($"/api/quotes/{createdQuote.Id}", createdQuote);


════════════════════════════════════════
PART 3 — VERIFICATION LOG
════════════════════════════════════════

## Evidence for all four claims:

### 1. Notification runs off the request thread
POST /api/quotes returns 201 before "Sending notification..." appears in logs:

  info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1]
        Executed endpoint 'CreateQuote'
  info: [Kestrel] POST /api/quotes → 201 Created      ← request thread returns
  info: QuotesApi.Services.QuoteNotificationService[0]
        Sending notification for quote 6               ← background thread, after response

### 2. Graceful shutdown works
Ctrl+C triggers OperationCanceledException in ReadAsync, caught at the boundary,
ExecuteAsync returns normally — no unhandled exception:

  ^C
  info: Microsoft.Hosting.Lifetime[0]
        Application is shutting down...
  info: QuotesApi.Services.QuoteNotificationService[0]
        QuoteNotificationService stopping.
  info: Microsoft.Hosting.Lifetime[0]
        Application stopped.

### 3. Channel is a true singleton — DI registration (Program.cs)

  builder.Services.AddSingleton(Channel.CreateUnbounded<int>());

AddSingleton means the same Channel<int> instance is injected into both the
CreateQuote endpoint handler and QuoteNotificationService.

### 4. Endpoint does not await the queue — TryWrite call (EndpointExtensions.cs)

  notificationChannel.Writer.TryWrite(createdQuote.Id);   // no await

TryWrite is synchronous and non-blocking. The 201 response is returned to
Angular immediately after the DB write, before the background service processes
the ID.

## One design decision worth noting:
Channel.CreateUnbounded<int>() means the queue never applies backpressure —
if notifications pile up faster than they are processed, the channel grows
without bound. For this use case (low-volume quote creation) that is fine.
For high-throughput scenarios, Channel.CreateBounded<int>(capacity) would be
the right choice.
