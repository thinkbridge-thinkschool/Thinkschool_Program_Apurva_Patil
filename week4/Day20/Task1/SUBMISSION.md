## Deliverable 5 — Crash Proof

### Outbox table

`OutboxMessages` lives in the same SQL Server database as `Quotes`.
One row per pending event; `ProcessedAt = NULL` means not yet published.
`Attempts` counts how many times the relay has tried — incremented and committed **before** each publish attempt so the counter is durable across crashes.

```sql
CREATE TABLE OutboxMessages (
    Id           uniqueidentifier NOT NULL PRIMARY KEY,
    EventType    nvarchar(100)    NOT NULL,
    Payload      nvarchar(max)    NOT NULL,
    CreatedAt    datetime2        NOT NULL,
    ProcessedAt  datetime2        NULL,         -- NULL = unsent
    Attempts     int              NOT NULL DEFAULT 0
);
CREATE INDEX IX_OutboxMessages_ProcessedAt ON OutboxMessages (ProcessedAt);
```

### Relay (RelayService.cs)

```csharp
var pending = await db.OutboxMessages
    .Where(m => m.ProcessedAt == null)
    .ToListAsync(ct);

foreach (var outbox in pending)
{
    // 1. Increment attempt counter and commit BEFORE publishing
    outbox.Attempts++;
    await db.SaveChangesAsync(ct);

    // 2. Publish — MessageId is stable across re-publishes of the same row
    var message = new ServiceBusMessage(outbox.Payload)
    {
        MessageId = outbox.Id.ToString(),
        ContentType = "application/json"
    };
    message.ApplicationProperties["eventType"] = outbox.EventType;
    await sender.SendMessageAsync(message, ct);

    // 3. CRASH WINDOW — broker has the message, ProcessedAt still NULL
    if (_crashAfterPublish)
        Environment.Exit(1);   // hard process kill, not a caught exception

    // 4. Mark sent
    outbox.ProcessedAt = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);
}
```

The ordering is deliberate: **publish, then mark**. A crash in the window between steps 2 and 4 leaves `ProcessedAt = NULL`, so the relay re-publishes on the next run (at-least-once). The consumer deduplicates via `ProcessedMessages` keyed on `MessageId` (= `OutboxMessage.Id`, stable across re-publishes).

`CrashAfterPublish` in `appsettings.json` controls the crash toggle. When `true`, the relay calls `Environment.Exit(1)` immediately after `SendMessageAsync` — a hard process termination, not a caught exception.

### Crash scenario tested — double crash (attempts climb to 3)

| Run | What happened | DB state after |
|-----|--------------|----------------|
| 1 | Relay incremented Attempts → 1, published, `Environment.Exit(1)` fired | Attempts=1, ProcessedAt=NULL |
| 2 | App restarted, relay found same row (still NULL), Attempts → 2, published, crashed again | Attempts=2, ProcessedAt=NULL |
| 3 | App restarted, `CrashAfterPublish=false`, Attempts → 3, published, ProcessedAt set | Attempts=3, ProcessedAt=filled |

**Run 1 — first crash**
1. `POST /api/quotes` created Quote + OutboxMessage in one explicit transaction (`BeginTransactionAsync` / `CommitAsync`). ProcessedAt = NULL, Attempts = 0.
2. Relay polled. Incremented Attempts → 1, committed.
3. Relay published to Service Bus (MessageId = `<outbox-id>`).
4. `Environment.Exit(1)` fired — process terminated. ProcessedAt still NULL.

**Run 2 — second crash**
5. App restarted. Relay polled. Row still ProcessedAt = NULL, Attempts = 1.
6. Incremented Attempts → 2, committed.
7. Relay published again (same MessageId).
8. `Environment.Exit(1)` fired again. ProcessedAt still NULL.

**Run 3 — recovery**
9. App restarted. Relay polled. Row still ProcessedAt = NULL, Attempts = 2.
10. Incremented Attempts → 3, committed.
11. Relay published again (same MessageId, attempt #3).
12. `CrashAfterPublish = false` — no crash. ProcessedAt = UtcNow committed.
13. Log: `Relay marked <id> as processed (attempt 3)`.

### Why no message is lost

The OutboxMessage row is written in the same explicit EF Core transaction as the Quote row (`BeginTransactionAsync` / `CommitAsync`). If the relay crashes before committing `ProcessedAt`, the row stays `ProcessedAt = NULL`. On every restart the relay queries `WHERE ProcessedAt IS NULL`, so the message is guaranteed to be re-published until it is successfully marked Sent. `Attempts = 3` in the final row is direct evidence that the retry ran three times.

### Why no message is double-applied

The consumer checks `ProcessedMessages` before processing any incoming message. `MessageId` is set to `OutboxMessage.Id` and is **stable** across all re-publishes of the same row. When the relay re-published the same row on runs 2 and 3, the consumer queried `ProcessedMessages WHERE MessageId = @id`, found the record written on delivery #1, and skipped the side-effect. The same event was published three times but applied exactly once.

### Evidence

**Single-crash (original test)**
- Screenshots/output-before-crash.png — console showing relay publishing before crash
- Screenshots/outbox-before-crash.png — DB: Attempts = 0, ProcessedAt = NULL before crash
- Screenshots/crash-log.png — console showing `SIMULATED CRASH` + process exit
- Screenshots/output-after-crash-null.png — DB: ProcessedAt still NULL after crash
- Screenshots/recovery-log.png — console showing relay re-published and marked sent
- Screenshots/outbox-after-recovery.png — DB: ProcessedAt filled after recovery
- Screenshots/outbox-after-fix.png — DB: final state after fix

**Double-crash (attempts climb to 3)**
- Screenshots/crash-run-1.png — console: attempt 1 published, `SIMULATED CRASH`, process exits
- Screenshots/outbox-attempt-1.png — DB: Attempts = 1, ProcessedAt = NULL after run 1
- Screenshots/crash-run-2.png — console: attempt 2 published, `SIMULATED CRASH`, process exits
- Screenshots/outbox-attempt-2.png — DB: Attempts = 2, ProcessedAt = NULL after run 2
- Screenshots/recovery2-log.png — console: attempt 3 published, relay marked as processed
- Screenshots/outbox-attempt-3.png — DB: Attempts = 3, ProcessedAt filled after recovery
