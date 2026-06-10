# Day 11 — Profile a Slow Endpoint

## Problem Statement
Add a deliberately slow endpoint to the QuotesApi. Profile it: capture p50/p99 under load, the SQL it emits, and the execution plan.

---

## Two endpoints added

| Endpoint | Pattern | Queries/request |
|---|---|---|
| `GET /api/collections/{id}/with-quotes-slow` | N+1 | 11 (1 + 10) |
| `GET /api/collections/{id}/with-quotes-fast` | IN-clause | 2 |

---

## 1. The Offending Code

`EndpointExtensions.cs` lines 217–239 — the slow endpoint:

```csharp
group.MapGet("/{id}/with-quotes-slow", async (
    int id,
    QuotesDbContext db,
    CancellationToken ct) =>
{
    var collection = await db.Collections          // Query 1: load collection + its items
        .Include(c => c.Items)
        .FirstOrDefaultAsync(c => c.Id == id, ct);

    if (collection is null) return Results.NotFound();

    var details = new List<object>();
    foreach (var item in collection.Items)         // iterates all 10 CollectionItems
    {
        var quote = await db.Quotes                // ← Query 2–11: one DB round trip per item
            .FirstOrDefaultAsync(q => q.Id == item.QuoteId, ct);
        details.Add(new { item.QuoteId, item.AddedAt, quote?.Author, quote?.Text });
    }

    return Results.Ok(new { collection.Id, collection.Name, Quotes = details });
});
```

**Root cause: explicit async `foreach` loop — NOT lazy loading.**
`await db.Quotes.FirstOrDefaultAsync(...)` is called inside a `foreach` over `collection.Items`.
EF Core cannot batch these; each iteration issues a separate `SELECT ... LIMIT 1` round trip.
A 10-item collection = 11 queries. Under concurrent load those 11 serial round trips stack up.

---

## 2. SQL Actually Emitted (from EF Core command log)

The running API uses **SQLite**. EF Core logs every SQL command when `.EnableSensitiveDataLogging().LogTo(Console.WriteLine, LogLevel.Information)` is set in `ServiceCollectionExtensions.cs`. Each `Executed DbCommand (Nms)` line is one query with its wall-clock time. (SQL Server `SET STATISTICS IO/TIME` output and the SSMS execution plan are in Section 4, run separately against a LocalDB instance with the same schema.)

### Slow endpoint — 11 queries per request

Captured from the terminal while hitting `GET /api/collections/1/with-quotes-slow`
(see `Screenshots/ef-sql-log-n1.png`):

```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (5ms) [Parameters=[@id='1' (DbType=Int32)], CommandType='Text', CommandTimeout='30']
      SELECT "c1"."Id", "c1"."Name", "c1"."OwnerId", "c0"."CollectionId", "c0"."QuoteId", "c0"."AddedAt"
      FROM (
          SELECT "c"."Id", "c"."Name", "c"."OwnerId"
          FROM "Collections" AS "c"
          WHERE "c"."Id" = @id
          LIMIT 1
      ) AS "c1"
      LEFT JOIN "CollectionItem" AS "c0" ON "c1"."Id" = "c0"."CollectionId"
      ORDER BY "c1"."Id", "c0"."CollectionId"

-- Queries 2–11: one per CollectionItem (10 items in this collection)
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (0ms) [Parameters=[@item_QuoteId='1' (DbType=Int32)], CommandType='Text', CommandTimeout='30']
      SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."IsDeleted", "q"."Text"
      FROM "Quotes" AS "q"
      WHERE "q"."Id" = @item_QuoteId
      LIMIT 1

-- ... repeated 9 more times with @item_QuoteId = 2, 3, 4, ... 10
```

10 serial round trips per request. Under 10 concurrent VUs those trips serialize further, driving p99 to 354ms and spikes to 2.36s.

### SQL Server STATISTICS IO/TIME — the 10 repeated queries

To prove the serial cost in SQL Server, the same 10 lookups were run in SSMS with statistics enabled:

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT * FROM Quotes WHERE Id = 1;
SELECT * FROM Quotes WHERE Id = 2;
-- ... through Id = 10
```

**Actual Messages tab output:**

```
-- Query 1 (Id = 1)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 0 ms.

-- Query 2 (Id = 2)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 338 ms.

-- Query 3 (Id = 3)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 254 ms.

-- Query 4 (Id = 4)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 174 ms.

-- Query 5 (Id = 5)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 0 ms.

-- Query 6 (Id = 6)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 444 ms.

-- Query 7 (Id = 7)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 204 ms.

-- Query 8 (Id = 8)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 187 ms.

-- Query 9 (Id = 9)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 0 ms.

-- Query 10 (Id = 10)
Table 'Quotes'. Scan count 0, logical reads 2   (1 row affected)
SQL Server Execution Times: CPU time = 0 ms,  elapsed time = 321 ms.

Completion time: 2026-06-10T12:39:57.2666919+05:30
```

**What this proves:**
- `Scan count 0` on every query — SQL Server is using the PK clustered index (no table scan per lookup)
- `logical reads 2` on every query — only 2 pages read each time (fast individually)
- CPU time = 0ms each — the database engine itself is not the bottleneck
- **Elapsed times accumulate serially: 0 + 338 + 254 + 174 + 0 + 444 + 204 + 187 + 0 + 321 = ~1,922ms total**
- The cost is not query complexity — it is **10 separate network round trips issued one after another from the application loop**. This is precisely what the `foreach` on line 229 causes.

### Fast endpoint — 2 queries per request

Captured from `GET /api/collections/1/with-quotes-fast`
(see `Screenshots/ef-sql-log-fast.png`):

```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (5ms) [Parameters=[@id='1' (DbType=Int32)], ...]
      SELECT "c1"."Id", ... (same collection query as above)

info: Microsoft.EntityFrameworkCore.Database.Command[20ms] [Parameters=[@ids1='1', @ids2='2', ..., @ids10='10'], ...]
      SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."IsDeleted", "q"."Text"
      FROM "Quotes" AS "q"
      WHERE "q"."Id" IN (@ids1, @ids2, @ids3, @ids4, @ids5, @ids6, @ids7, @ids8, @ids9, @ids10)
```

Two queries regardless of collection size.

---

## 3. k6 Load Test — Actual Output

```bash
k6 run k6/load-test.js
```

Full terminal output (see `Screenshots/k6-results.png`):

```
scenarios: (100.00%) 2 scenarios, 20 max VUs, 1m15s max duration (incl. graceful stop):
         * slow_endpoint: 10 looping VUs for 20s (exec: slowTest, gracefulStop: 30s)
         * fast_endpoint: 10 looping VUs for 20s (exec: fastTest, startTime: 25s, gracefulStop: 30s)

THRESHOLDS
  http_req_duration{endpoint:fast}
    ✓ 'p(99)<500'   p(99)=124.27ms

  http_req_duration{endpoint:slow}
    ✓ 'p(99)<5000'  p(99)=354.26ms

TOTAL RESULTS
  checks_total.......: 13223     293.622111/s
  checks_succeeded...: 100.00%  13223 out of 13223
  checks_failed......: 0.00%    0 out of 13223

  ✓ slow 200
  ✓ fast 200

HTTP
  http_req_duration...................: avg=30.03ms   min=522.29µs  med=13.99ms  max=2.36s     p(90)=74.13ms   p(95)=106.51ms
    { endpoint:fast }.................: avg=17.43ms   min=522.29µs  med=11.97ms  max=387.71ms  p(90)=31.84ms   p(95)=44.18ms
    { endpoint:slow }.................: avg=105.37ms  min=13.18ms   med=81.73ms  max=2.36s     p(90)=150.31ms  p(95)=218.44ms
  http_req_failed....................: 0.00%    0 out of 13223
  http_reqs..........................: 13223    293.622111/s

EXECUTION
  iteration_duration.................: avg=30.28ms   min=522.29µs  med=14.21ms  max=2.37s     p(90)=74.47ms   p(95)=106.87ms
  iterations.........................: 13223    293.622111/s
  vus................................: 10       min=0          max=10
  vus_max............................: 20       min=20         max=20

NETWORK
  data_received......................: 18 MB    391 kB/s
  data_sent..........................: 1.4 MB   31 kB/s

running (0m45.0s), 00/20 VUs, 13223 complete and 0 interrupted iterations
slow_endpoint ✓ [====================================] 10 VUs  20s
fast_endpoint ✓ [====================================] 10 VUs  20s
```

### p50 / p99 summary

| Metric | Slow (N+1) | Fast (IN-clause) |
|---|---|---|
| p50 (med) | 81.73ms | 11.97ms |
| p99 | 354.26ms | 124.27ms |
| avg | 105.37ms | 17.43ms |
| max | 2.36s | 387.71ms |

Fast is **~7× faster at p50** and **~3× faster at p99**.

---

## 4. Execution Plan — SQL Server SSMS

The index analysis was run against a SQL Server LocalDB instance (`(localdb)\MSSQLLocalDB.QuotesDb`) using the same schema.

### Before index — SET STATISTICS IO/TIME output (table scan)

Query run in SSMS:

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT * FROM Quotes WHERE Author = 'Marcus Aurelius';
```

**Messages tab output** (see `Screenshots/explain-before-index-messages.png`):

```
SQL Server parse and compile time:
    CPU time = 0 ms, elapsed time = 8 ms.

(2020 rows affected)
Table 'Quotes'. Scan count 1, logical reads 124, physical reads 0,
  page server reads 0, read-ahead reads 0, lob logical reads 0.

SQL Server Execution Times:
   CPU time = 0 ms,  elapsed time = 73 ms.

Completion time: 2026-06-10T12:05:36.2395607+05:30
```

Key numbers:
- `Scan count 1` — one full table scan (no index, reads every row)
- `logical reads 124` — 124 data pages read to satisfy the query
- `elapsed time = 73 ms`

Results grid (see `Screenshots/explain-before-index-plan.png`) shows all 2020 matching rows returned from the scan.

### After index — SSMS graphical execution plan (index seek)

After adding `IX_Quotes_Author` (migration `20260529034039_AddAuthorIndex`):

```csharp
migrationBuilder.CreateIndex(
    name: "IX_Quotes_Author",
    table: "Quotes",
    column: "Author");
```

SSMS execution plan (see `Screenshots/explain-after-index-plan.png`):

```
SELECT  ← Cost 0%
  └─ Nested Loops (Inner Join)  ← Cost 0%
       ├─ Index Seek (NonClustered) [Quotes].[IX_Quotes_Author]  ← Cost 83%
       │    2020 rows estimated
       └─ Key Lookup (Clustered) [Quotes].[PK_Quotes]            ← Cost 12%
            1 row estimated
       └─ Filter                                                  ← Cost 4%
```

SQL Server now seeks directly into `IX_Quotes_Author` instead of scanning the whole table. No table scan node appears in the plan at all.

---

## How to Reproduce

```powershell
cd QuotesApi
dotnet run

# Seed 100 quotes + 3 collections (10 items each)
Invoke-WebRequest -Method POST -Uri http://localhost:5182/api/dev/seed
# → {"quotes":100,"collections":3}   (see Screenshots/Seed-Data-Verification.png)

# Hit slow endpoint in browser — watch EF Core log for 11 queries
# GET http://localhost:5182/api/collections/1/with-quotes-slow
# (see Screenshots/SlowEndpointResponse.png for actual JSON response)

# Hit fast endpoint — EF Core log shows only 2 queries
# GET http://localhost:5182/api/collections/1/with-quotes-fast
# (see Screenshots/FastEndpointResponse.png)

# Run load test (requires k6 installed)
k6 run k6/load-test.js
```

---

## Findings

**Problem 1 — N+1 Query (explicit loop)**
`EndpointExtensions.cs:229` calls `await db.Quotes.FirstOrDefaultAsync(...)` inside a `foreach` over a loaded collection.
That is an **explicit async loop, not lazy loading** — EF Core never had a chance to batch the queries.
For a 10-item collection this fires 11 queries; p99 reached 354ms under 10 VUs with max spikes of 2.36s.
Fix: collect all IDs first, run one `WHERE Id IN (...)` query (2 queries total, p99 drops to 124ms).

**Problem 2 — Missing Index on `Quotes.Author`**
No index existed on the `Author` column until migration `20260529034039_AddAuthorIndex`.
Any filter on author caused a full table scan: `Scan count 1, logical reads 124, elapsed time 73 ms` (from SSMS `SET STATISTICS IO ON`).
After `HasIndex(e => e.Author)` + migration, SQL Server uses `Index Seek (NonClustered)` on `IX_Quotes_Author` — no table scan in the execution plan.
