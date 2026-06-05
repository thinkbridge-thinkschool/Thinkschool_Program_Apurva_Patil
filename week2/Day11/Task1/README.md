# Day 11 — Profile a Slow Endpoint

## Problem Statement
Add a deliberately slow endpoint to the Week-1 QuotesApi. Profile it: capture p50/p99 under load, the SQL it emits, and the execution plan.

---

## What Was Built

### Two performance problems demonstrated

#### 1. N+1 Query Problem
**Endpoint:** `GET /api/collections/{id}/with-quotes-slow`

The naive implementation loads a collection (1 query), then fires a separate `SELECT` per `CollectionItem` to fetch its quote — 1 + N queries for a single request.

**Slow SQL pattern (1 + 10 queries for a 10-item collection):**
```sql
-- Query 1: load collection
SELECT "c"."Id", "c"."Name" FROM "Collections" WHERE "c"."Id" = @id LIMIT 1

-- Queries 2–11: one per item (N+1)
SELECT "q"."Id", "q"."Author" FROM "Quotes" WHERE "q"."Id" = @item_QuoteId LIMIT 1
SELECT "q"."Id", "q"."Author" FROM "Quotes" WHERE "q"."Id" = @item_QuoteId LIMIT 1
... (repeated 10 times)
```

**Fix:** `GET /api/collections/{id}/with-quotes-fast` — loads all quotes in a single `WHERE Id IN (...)` query (2 queries total regardless of collection size).

---

#### 2. Missing Index on Author Column
**Column:** `Quotes.Author`

No index existed on the `Author` column. Any filter on author caused a full table scan.

**Before index:**
```
EXPLAIN QUERY PLAN SELECT * FROM Quotes WHERE Author = 'Marcus Aurelius';
QUERY PLAN
`--SCAN Quotes
```

**After index (`HasIndex(e => e.Author)` + migration):**
```
QUERY PLAN
`--SEARCH Quotes USING INDEX IX_Quotes_Author (Author=?)
```

---

## Folder Structure

```
Task1/
├── QuotesApi/        — full runnable API with slow + fast endpoints added
├── k6/
│   ├── load-test.js  — k6 script targeting slow and fast endpoints
│   ├── slow-results.txt  — p50/p99 output for slow endpoint
│   └── fast-results.txt  — p50/p99 output for fast endpoint
├── sql/
│   ├── profiling.sql     — EXPLAIN QUERY PLAN commands
│   └── fix-add-index.sql — index fix SQL
├── Screenshots/      — EF Core SQL logs + EXPLAIN output + k6 results
└── README.md
```

---

## How to Run

```bash
cd QuotesApi
dotnet run

# Seed 100 quotes + 3 collections
Invoke-WebRequest -Method POST -Uri http://localhost:5182/api/dev/seed

# Hit slow endpoint (watch terminal for N+1 SQL)
# GET http://localhost:5182/api/collections/1/with-quotes-slow

# Hit fast endpoint (watch terminal for 2 queries)
# GET http://localhost:5182/api/collections/1/with-quotes-fast

# Run load test
k6 run k6/load-test.js
```

---

## Key Findings

| Metric | Slow (N+1) | Fast (IN-clause) |
|---|---|---|
| Queries per request | 11 (1 + 10) | 2 |
| p50 latency | 81.73ms | 11.97ms |
| p99 latency | 354.26ms | 124.27ms |
| avg latency | 105.37ms | 17.43ms |
| max latency | 2.36s | 387.71ms |
| Author lookup | SCAN (full table) | SEARCH (index) |

> Proof: `Screenshots/k6-results.png`, `Screenshots/ef-sql-log-n1.png`, `Screenshots/ef-sql-log-fast.png`, `Screenshots/explain-query-plan.png`

---

## Baseline p50/p99 (10 VUs, 20s duration each)

**Slow endpoint** `GET /api/collections/{id}/with-quotes-slow`
- p50 = **81.73ms**
- p99 = **354.26ms**
- max = **2.36s**

**Fast endpoint** `GET /api/collections/{id}/with-quotes-fast`
- p50 = **11.97ms**
- p99 = **124.27ms**
- max = **387.71ms**

Fast is **~6x faster** at p50 and p99.

---

## Offending SQL (captured from EF Core logs)

Every request to the slow endpoint emits **11 queries**:

```sql
-- Query 1: load the collection
SELECT "c1"."Id", "c1"."Name", "c1"."OwnerId", "c0"."CollectionId", "c0"."QuoteId", "c0"."AddedAt"
FROM "Collections" AS "c"
WHERE "c"."Id" = @id
LIMIT 1

-- Queries 2–11: one per CollectionItem (N+1)
SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."IsDeleted", "q"."Text"
FROM "Quotes" AS "q"
WHERE "q"."Id" = @item_QuoteId
LIMIT 1

-- ... repeated 10 times, once per quote in the collection
```

The fast endpoint emits only **2 queries**:

```sql
-- Query 1: same collection load
SELECT ... FROM "Collections" WHERE "c"."Id" = @id LIMIT 1

-- Query 2: all quotes in one shot
SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."IsDeleted", "q"."Text"
FROM "Quotes" AS "q"
WHERE "q"."Id" IN (@ids1, @ids2, @ids3, @ids4, @ids5, @ids6, @ids7, @ids8, @ids9, @ids10)
```

---

## Execution Plan

```sql
EXPLAIN QUERY PLAN SELECT * FROM Quotes WHERE Author = 'Marcus Aurelius';
```

**Before index (full table scan):**
```
QUERY PLAN
`--SCAN Quotes
```

**After `HasIndex(e => e.Author)` + migration:**
```
QUERY PLAN
`--SEARCH Quotes USING INDEX IX_Quotes_Author (Author=?)
```

---

## Two Biggest Problems Found

**Problem 1 — N+1 Query**
Loading a 10-item collection fired 11 separate SQL round trips. Under 10 concurrent users this caused p99 to reach 354ms and occasional spikes to 2.36s. The fix is a single `WHERE Id IN (...)` query — 2 queries total, p99 drops to 124ms.

**Problem 2 — Missing Index on `Quotes.Author`**
The `Author` column had no index. Every query filtering by author performed a full table scan (`SCAN Quotes`), reading every row regardless of how many matched. Fixed by adding `HasIndex(e => e.Author)` to `QuotesDbContext` and running a migration — SQLite now uses `SEARCH Quotes USING INDEX IX_Quotes_Author`.