# Solution — Drop p99 by 10×

## Test Scope

- **Collection size:** N = 100 items (Collection-1, seeded via `POST /api/dev/seed`)
- **Load:** up to 80 virtual users, 50 s ramp (k6)
- **Slow path:** 101 SQL queries per request (1 for collection + 100 individual quote fetches)
- **Fast path:** 2 SQL queries per request (1 for collection + 1 IN-clause for all 100 IDs)

---

## Before/After p99

| Metric | Before (N+1, N=100, 80 VU) | After (IN-clause, N=100, 80 VU) | Improvement |
|---|---|---|---|
| p50 | 4140 ms | 249 ms | **16.6×** |
| p99 | 5900 ms | 590 ms | **10×** |
| Throughput | 16.4 req/s | 274 req/s | **16.7×** |
| Total requests (50 s) | 822 | 13,702 | **16.7×** |
| Queries per request | 101 | 2 | **50.5× fewer** |

---

## k6 Output — Before (N+1, 80 VU)

```
     scenarios: (100.00%) 1 scenario, 80 max VUs, 1m20s max duration (incl. graceful stop):
              * default: Up to 80 looping VUs for 50s over 3 stages (gracefulRampDown: 30s, gracefulStop: 30s)

  █ THRESHOLDS

    http_req_duration
    ✓ 'p(50)<10000' p(50)=4.14s
    ✓ 'p(99)<20000' p(99)=5.9s

    http_req_failed
    ✓ 'rate<0.01' rate=0.00%

  █ TOTAL RESULTS

    checks_total.......: 822     16.408862/s
    checks_succeeded...: 100.00% 822 out of 822
    checks_failed......: 0.00%   0 out of 822

    ✓ status 200

    HTTP
    http_req_duration..............: avg=4.01s min=106.98ms med=4.14s max=6.1s p(90)=5.51s p(95)=5.69s
      { expected_response:true }...: avg=4.01s min=106.98ms med=4.14s max=6.1s p(90)=5.51s p(95)=5.69s
    http_req_failed................: 0.00%  0 out of 822
    http_reqs......................: 822    16.408862/s

    EXECUTION
    iteration_duration.............: avg=4.01s min=106.98ms med=4.14s max=6.1s p(90)=5.51s p(95)=5.69s
    iterations.....................: 822    16.408862/s
    vus............................: 1      min=1        max=80
    vus_max........................: 80     min=80       max=80

    NETWORK
    data_received..................: 9.5 MB 190 kB/s
    data_sent......................: 86 kB  1.7 kB/s

running (0m50.1s), 00/80 VUs, 822 complete and 0 interrupted iterations
default ✓ [======================================] 00/80 VUs  50s
```

## k6 Output — After (IN-clause, 80 VU)

```
     scenarios: (100.00%) 1 scenario, 80 max VUs, 1m20s max duration (incl. graceful stop):
              * default: Up to 80 looping VUs for 50s over 3 stages (gracefulRampDown: 30s, gracefulStop: 30s)

  █ THRESHOLDS

    http_req_duration
    ✓ 'p(50)<500' p(50)=248.75ms
    ✓ 'p(99)<1000' p(99)=590.08ms

    http_req_failed
    ✓ 'rate<0.01' rate=0.00%

  █ TOTAL RESULTS

    checks_total.......: 13702   274.028591/s
    checks_succeeded...: 100.00% 13702 out of 13702
    checks_failed......: 0.00%   0 out of 13702

    ✓ status 200

    HTTP
    http_req_duration..............: avg=234.17ms min=5.37ms med=248.75ms max=778.67ms p(90)=381.78ms p(95)=426.86ms
      { expected_response:true }...: avg=234.17ms min=5.37ms med=248.75ms max=778.67ms p(90)=381.78ms p(95)=426.86ms
    http_req_failed................: 0.00%  0 out of 13702
    http_reqs......................: 13702  274.028591/s

    EXECUTION
    iteration_duration.............: avg=234.4ms  min=5.79ms med=249.11ms max=778.67ms p(90)=381.98ms p(95)=427.13ms
    iterations.....................: 13702  274.028591/s
    vus............................: 1      min=1          max=80
    vus_max........................: 80     min=80         max=80

    NETWORK
    data_received..................: 159 MB 3.2 MB/s
    data_sent......................: 1.4 MB 29 kB/s

running (0m50.0s), 00/80 VUs, 13702 complete and 0 interrupted iterations
default ✓ [======================================] 00/80 VUs  50s
```

---

## Changes Made

### 1. Eliminated the N+1 — single IN-clause query

**File:** `QuotesApi/Extensions/EndpointExtensions.cs` — `GetCollectionWithQuotesFast` endpoint

```csharp
// BEFORE — 1 query per item (N+1), 101 round-trips for N=100
foreach (var item in collection.Items)
{
    var quote = await db.Quotes
        .FirstOrDefaultAsync(q => q.Id == item.QuoteId, ct);
}

// AFTER — 1 query for all items, always 2 round-trips regardless of N
var ids = collection.Items.Select(i => i.QuoteId).ToList();
var quotes = await db.Quotes
    .Where(q => ids.Contains(q.Id))
    .ToDictionaryAsync(q => q.Id, ct);
```

EF Core translates `.Contains()` into `WHERE Id IN (...)`.
SQL Server resolves the entire set in one clustered index seek.

### 2. Covering index (migration: `AddCoveringAuthorIndex`)

**File:** `QuotesApi/Migrations/20260529140313_AddCoveringAuthorIndex.cs`

```sql
-- BEFORE: narrow index — Key Lookup required for Text + CreatedAt
CREATE INDEX IX_Quotes_Author ON Quotes (Author);

-- AFTER: covering index — Text + CreatedAt live in the index leaf page
CREATE NONCLUSTERED INDEX IX_Quotes_Author
    ON Quotes (Author ASC)
    INCLUDE (Text, CreatedAt);
```

EF Core model config (`QuotesDbContext.cs`):
```csharp
entity.HasIndex(e => e.Author)
      .HasDatabaseName("IX_Quotes_Author")
      .IncludeProperties(nameof(Quote.Text), nameof(Quote.CreatedAt));
```

---

## Before/After Execution Plans (SSMS — STATISTICS IO)

### N+1 path — single quote lookup (runs 100× per request)

```
=== BEFORE: single-row quote fetch (runs 100× per request in N+1 loop) ===

(1 row affected)
Table 'Quotes'. Scan count 0, logical reads 2, physical reads 2, page server reads 0,
read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0.
SQL Server Execution Times: CPU time = 0 ms, elapsed time = 0 ms.

Total cost: 2 logical reads × 100 iterations = 200 logical reads, 100 DB round trips.
```

### IN-clause path — all 100 quotes in one query

```
=== AFTER: single IN-clause for all 100 quote IDs ===

(100 rows affected)
Table 'Quotes'. Scan count 1, logical reads 4, physical reads 0, page server reads 0,
read-ahead reads 1, page server read-ahead reads 0, lob logical reads 0.
SQL Server Execution Times: CPU time = 0 ms, elapsed time = 241 ms.

Total cost: 4 logical reads, 1 DB round trip.
```

### Author-filtered query — covering index eliminates Key Lookup

```
=== Author-filtered query — should use IX_Quotes_Author (no key lookup) ===

(20 rows affected)
Table 'Quotes'. Scan count 1, logical reads 3, physical reads 0, page server reads 0,
read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0.
SQL Server Execution Times: CPU time = 0 ms, elapsed time = 0 ms.
Completion time: 2026-06-11T17:28:52.0580918+05:30

Result: 3 logical reads (covering index — no key lookup needed).
```

---

## Files

| File | Purpose |
|---|---|
| `k6/before.js` | Load test for the slow (N+1) endpoint, N=100, 80 VU |
| `k6/after.js` | Load test for the fast (IN-clause) endpoint, N=100, 80 VU |
| `sql/profiling.sql` | Run in SSMS to capture real STATISTICS IO output |
| `QuotesApi/Migrations/20260529140313_AddCoveringAuthorIndex.cs` | Covering index migration |

---

## How to Re-run and Complete This Document

### Step 1 — Start the app
```
cd QuotesApi
dotnet run
```

### Step 2 — Seed the database
```
POST http://localhost:5182/api/dev/seed
```
Verify: `GET http://localhost:5182/api/collections/1/with-quotes-slow` → should show 100 quotes.

### Step 3 — Run the before test, copy full terminal output
```
k6 run k6/before.js
```
Paste the complete k6 terminal output (from THRESHOLDS through NETWORK) into the "k6 Output — Before" section above.

### Step 4 — Run the after test, copy full terminal output
```
k6 run k6/after.js
```
Paste the complete k6 terminal output into the "k6 Output — After" section above.

### Step 5 — Fill in the summary table
Take p50 and p99 from each run and compute the ratio.

### Step 6 — Run SSMS profiling
Open `sql/profiling.sql` in SSMS against QuotesDb, run each section, copy the Messages tab text output into the execution plan sections above.

---

## Evidence Screenshots

### Seed verification — N=100
![Seed response showing itemsInCollection1: 100](screenshots/Postman-Seed-100.png)

### k6 Before — 80 VUs, N+1 path (p99=5.9s)
![k6 before output](screenshots/k6-before-output.png)

### k6 After — 80 VUs, IN-clause path (p99=590ms)
![k6 after output](screenshots/After_k6Output.png)

### SSMS logical reads — before vs after
![SSMS profiling output](screenshots/ssms-profiling-output.png)
