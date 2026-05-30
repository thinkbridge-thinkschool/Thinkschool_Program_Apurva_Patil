# Day 12 — When to Reach for Dapper

## The rule

> Default to EF Core. Reach for Dapper only when a read path is both hot — called on nearly every request — and purely projective — pure SELECT to DTO with no writes, no change tracking, and no navigation properties. Even then, Dapper's per-request advantage (lower p50/p95) only survives under concurrent load if you manage connections properly; opening a new `SqlConnection` per call exhausts the pool and kills throughput, as seen in the k6 results where EF handled 3× more requests. The SQL both generate is nearly identical — Dapper's edge is framework overhead, not a better query.

---

## Both implementations

### EF Core (`QuoteQueryService.cs`)

```csharp
return await _db.Quotes
    .Where(q => !q.IsDeleted)
    .OrderByDescending(q => q.CreatedAt)
    .Skip((page - 1) * size)
    .Take(size)
    .AsNoTracking()
    .Select(q => new QuoteReadModel(q.Id, q.Author, q.Text, q.CreatedAt))
    .ToListAsync(cancellationToken);
```

**SQL EF Core generates:**
```sql
SELECT [q].[Id], [q].[Author], [q].[Text], [q].[CreatedAt]
FROM   [Quotes] AS [q]
WHERE  [q].[IsDeleted] = 0
ORDER  BY [q].[CreatedAt] DESC
OFFSET @__p_0 ROWS FETCH NEXT @__p_1 ROWS ONLY
```

### Dapper (`DapperQuoteQueryService.cs`)

```csharp
await using var conn = new SqlConnection(_connectionString);
var rows = await conn.QueryAsync<QuoteReadModel>(
    new CommandDefinition(
        "SELECT Id, Author, Text, CreatedAt FROM Quotes WHERE IsDeleted = 0 ORDER BY CreatedAt DESC OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY",
        new { offset = (page - 1) * size, size },
        cancellationToken: cancellationToken));
return rows.ToList();
```

**The SQL is nearly identical.** Dapper's edge is not a better query — it skips the LINQ→SQL translation layer and DbContext state-machine on every call.

---

## Timing comparison

k6 load test — 20 virtual users, 5s ramp-up + 30s sustained + 5s ramp-down, 500 seeded rows.

On single-request latency, Dapper was faster — p50 was 4.1ms vs EF's 5.88ms, and p95 was 11.75ms vs 15.81ms. However, EF handled 3210 total requests at 80 req/s, while Dapper only handled 1059 at 18 req/s. The reason is connection management — our `DapperQuoteQueryService` opens a `new SqlConnection()` on every call. Under 20 concurrent users, this exhausts the LocalDB connection pool, causing some requests to wait up to 25 seconds for a free slot, which pushed Dapper's average to 952ms despite its 4.1ms median. EF's `DbContext` manages the pool automatically and handled the load cleanly. The fix would be reusing the existing connection via `_db.Database.GetDbConnection()` — which would give Dapper both the raw SQL speed and EF's connection pool benefits.

---

## Decision table

| Factor | EF Core | Dapper |
|---|---|---|
| LINQ → SQL overhead | Yes (amortised after first call) | None — you write the SQL |
| DbContext state-machine | Yes (even with `AsNoTracking`) | None |
| Mapping layer | EF materialiser | IL-emitted mapper (reused) |
| Migrations keep SQL in sync | Automatically | You update SQL manually |
| Handles connection pooling | Automatically | Manual — must reuse connection |
| When to pick | Everything else | Hot reads, pure projection, managed connection |
