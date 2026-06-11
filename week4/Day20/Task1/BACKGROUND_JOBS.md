# Background Jobs in ASP.NET Core

## IHostedService vs BackgroundService

`IHostedService` is the raw interface. It has two methods: `StartAsync(CancellationToken)` and `StopAsync(CancellationToken)`. You wire everything yourself — you decide how to start work, how to keep it running, and how to stop it. It is the right choice when your job has a very specific lifecycle that does not fit a simple loop (for example, subscribing to an event bus on start and unsubscribing on stop).

`BackgroundService` is an abstract class that implements `IHostedService` for you. It starts a `Task` that calls your `ExecuteAsync(CancellationToken)` override, and it cancels that token when the host is shutting down. You only need to write the loop. Pick `BackgroundService` for any job that runs continuously — polling a queue, draining a channel, or processing work in a loop. That covers almost all background job scenarios.

**Rule of thumb:** reach for `BackgroundService` by default. Drop down to raw `IHostedService` only when you need to control startup and shutdown independently, with no persistent loop in between.

---

## Where Hangfire Fits

`BackgroundService` and `Channel<T>` work well for fire-and-forget work that is in-process (the same app instance that enqueues the work also runs it), ephemeral (if the process restarts before the job runs, the job is lost), and simple (no retry, no scheduling, no dashboard).

Hangfire solves the problems that `BackgroundService` does not. Jobs written to Hangfire survive a process restart because they are persisted to a database — a `Channel<T>` lives only in memory. Hangfire has built-in configurable retry on failure; with `BackgroundService` you write that logic yourself. Recurring scheduled jobs are a first-class feature in Hangfire via cron expressions; with `BackgroundService` you wire a timer loop manually. Hangfire ships a web dashboard showing what ran, what failed, and what is pending; with `BackgroundService` you only have logs. Finally, Hangfire supports multiple worker instances all reading from the same persistent queue, which `BackgroundService` is not designed for.

Hangfire is the right tool when reliability matters: if "quote notification email was never sent because the pod restarted" is unacceptable, you need a persistent queue backed by a database. The tradeoff is an extra dependency, a database table, and a configured Hangfire server.

For development tooling and low-stakes side effects (audit logs, cache warming) that can be safely dropped, `BackgroundService` + `Channel<T>` is simpler and sufficient.

---

## Why the Cancellation Token Matters for Azure-Hosted Services

When Azure App Service restarts an instance — rolling deploy, scaling event, health-check failure, or `SIGTERM` from the platform — it sends a graceful shutdown signal before killing the process. ASP.NET Core translates this into cancellation: the `IHostedService.StopAsync` path is called, which cancels the token passed into `ExecuteAsync`.

If you ignore the token, `ReadAsync` will block forever waiting for the next item. The host cannot stop cleanly within its shutdown timeout (default 5 seconds on Azure) and the platform kills the process hard, potentially mid-operation.

If you handle the token correctly (as done here — catch `OperationCanceledException` outside the loop and return), `ExecuteAsync` exits promptly. The host finishes shutting down cleanly within the allowed window. On Azure this prevents torn in-flight work, unhandled exception logs polluting Application Insights, and deployment health checks incorrectly flagging the instance as unhealthy during a rolling restart.

The pattern is: let the token do its job, catch the cancellation at the boundary, and return normally.

---

## Evidence

### 1. Channel is a true singleton — DI registration (Program.cs)

```csharp
builder.Services.AddSingleton(Channel.CreateBounded<int>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait
}));
builder.Services.AddHostedService<QuoteNotificationService>();
```

`AddSingleton` means the same `Channel<int>` instance is injected into both `CreateQuote` (via Minimal API parameter binding) and `QuoteNotificationService` (via constructor injection). `BoundedChannelOptions(100)` with `FullMode = Wait` means the channel holds at most 100 pending IDs. If the queue fills up, `TryWrite` in the endpoint will block callers rather than growing memory unboundedly.

### 2. Controller does not await the queue — TryWrite call (EndpointExtensions.cs)

```csharp
var createdQuote = await repository.AddAsync(quote, cancellationToken);
notificationChannel.Writer.TryWrite(createdQuote.Id);   // no await — fire and forget
return Results.Created($"/api/quotes/{createdQuote.Id}", createdQuote);
```

`TryWrite` is synchronous. The `201 Created` response is returned to Angular immediately after the DB write, before the background service picks up the ID from the channel.

### 3. Notification runs off the request thread — actual log output

Observed console output after `POST /api/quotes`: