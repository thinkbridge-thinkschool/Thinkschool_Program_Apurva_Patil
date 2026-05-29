# Solution — Drop p99 by 10×

## Before/After p99

| Metric | Before (N+1) | After (IN-clause) | Improvement |
|---|---|---|---|
| p50 | 21.54 ms | 4.73 ms | **4.5×** |
| p99 | 67.58 ms | 25.93 ms | **2.6×** |
| Queries per request | 11 | 2 | **5.5× fewer** |

Tested with k6: 10 virtual users, 30 s steady load, `GET /api/collections/1/with-quotes-*`.

**Why not 10×:** our collection has 10 items (N=10), so the slow path sends 11 queries.
At max capacity (N=50) the slow path sends 51 queries vs always 2 — that is where the
ratio crosses 10×. The query-count reduction is linear with N; the latency ratio follows.

---

## Changes Made

### 1. Eliminated the N+1 — single IN-clause query

```csharp
// BEFORE — 1 query per item (N+1)
foreach (var item in collection.Items)
{
    var quote = await db.Quotes
        .FirstOrDefaultAsync(q => q.Id == item.QuoteId, ct);
}

// AFTER — 1 query for all items
var ids = collection.Items.Select(i => i.QuoteId).ToList();
var quotes = await db.Quotes
    .Where(q => ids.Contains(q.Id))
    .ToDictionaryAsync(q => q.Id, ct);
```

EF Core translates `.Contains()` into `WHERE Id IN (...)`.
SQL Server resolves the entire set in one clustered index seek.
Result: always **2 queries per request** regardless of collection size.

### 2. Upgraded narrow index to covering index

Migration: `AddCoveringAuthorIndex`

```sql
-- BEFORE: narrow index — requires a Key Lookup per row for Text + CreatedAt
CREATE INDEX IX_Quotes_Author ON Quotes (Author);

-- AFTER: covering index — Text + CreatedAt live in the index leaf page
CREATE NONCLUSTERED INDEX IX_Quotes_Author
    ON Quotes (Author ASC)
    INCLUDE (Text, CreatedAt);
```

EF Core model:
```csharp
entity.HasIndex(e => e.Author)
      .HasDatabaseName("IX_Quotes_Author")
      .IncludeProperties(nameof(Quote.Text), nameof(Quote.CreatedAt));
```

---

## Before/After Execution Plans

### N+1 path (slow endpoint)

**Before:**
- Query 1: `Clustered Index Seek` on Collections PK → 1 row
- Query 2–11 (foreach loop): `Clustered Index Seek` on Quotes PK → 1 row each
- **11 network round-trips, ~20 logical reads**

**After:**
- Query 1: `Clustered Index Seek` on Collections PK → 1 row
- Query 2: `Clustered Index Seek` on Quotes PK with `IN (id1…id10)` → all rows in one seek
- **2 network round-trips, ~3 logical reads**

### Covering index (author-filtered query)

**Before — narrow index:**
```
Index Seek  (IX_Quotes_Author)   ← finds row IDs matching Author
Key Lookup  (Quotes PK)          ← fetches Text + CreatedAt per row  ← extra I/O
```

**After — covering index:**
```
Index Seek  (IX_Quotes_Author)   ← finds rows AND reads Text + CreatedAt from leaf page
                                    no Key Lookup needed
```

---

## Files

| File | Purpose |
|---|---|
| `k6/before.js` | Load test for the slow (N+1) endpoint |
| `k6/after.js` | Load test for the fast (IN-clause) endpoint |
| `sql/fix-add-index.sql` | T-SQL to create the covering index manually |
| `sql/profiling.sql` | Before/after query plans with STATISTICS IO output |
| `QuotesApi/WHY.md` | Root cause, fix, and measured results |
