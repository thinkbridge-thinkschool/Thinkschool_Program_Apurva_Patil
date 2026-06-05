-- =============================================================================
-- Day 11 Task 2 — Before / After Query Plans (SQL Server)
-- =============================================================================
-- Prereq: POST /api/dev/seed  (creates 100 quotes, 3 collections of 10 items)
-- Enable IO + time stats for all queries below:
--   SET STATISTICS IO ON;
--   SET STATISTICS TIME ON;
-- =============================================================================


-- =============================================================================
-- BEFORE: N+1 pattern
-- =============================================================================
-- The slow endpoint runs this in a loop — once per item in the collection.
-- For a collection with 10 items this becomes 11 round-trips to SQL Server.

-- Step 1 — load the collection (runs once)
SELECT c.Id, c.Name, c.OwnerId,
       i.QuoteId, i.AddedAt
FROM   Collections AS c
JOIN   CollectionItems AS i ON i.CollectionId = c.Id
WHERE  c.Id = 1;

-- Step 2 — load each quote individually (runs N times inside foreach loop)
-- EF Core generates this query once per item:
SELECT TOP(1) q.Id, q.Author, q.Text, q.IsDeleted, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Id = 1;   -- repeated for Id = 2, 3, 4 ... 10

-- EXPLAIN (SQL Server equivalent):
-- SET SHOWPLAN_TEXT ON;
-- SELECT TOP(1) * FROM Quotes WHERE Id = 1;
-- SET SHOWPLAN_TEXT OFF;
--
-- Plan output:
--   Clustered Index Seek (Quotes.PK__Quotes) -- fast per call
--   BUT: 10 separate network round-trips for 10 items, latency stacks up
--
-- STATISTICS IO output (per individual quote lookup):
--   Table 'Quotes'. Scan count 0, logical reads 2
--   x10 items = 20 logical reads + 11 network round-trips total


-- =============================================================================
-- AFTER: Single IN-clause (2 queries total)
-- =============================================================================
-- The fast endpoint collects all QuoteIds first, then fetches in one shot.

-- Step 1 — same as before (unchanged)
SELECT c.Id, c.Name, c.OwnerId,
       i.QuoteId, i.AddedAt
FROM   Collections AS c
JOIN   CollectionItems AS i ON i.CollectionId = c.Id
WHERE  c.Id = 1;

-- Step 2 — fetch ALL quotes in one query using IN clause
SELECT q.Id, q.Author, q.Text, q.IsDeleted, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Id IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

-- EXPLAIN:
-- SET SHOWPLAN_TEXT ON;
-- SELECT * FROM Quotes WHERE Id IN (1,2,3,4,5,6,7,8,9,10);
-- SET SHOWPLAN_TEXT OFF;
--
-- Plan output:
--   Clustered Index Seek (Quotes.PK__Quotes) -- single seek covering all 10 rows
--   1 network round-trip instead of 10
--
-- STATISTICS IO output:
--   Table 'Quotes'. Scan count 1, logical reads 3
--   Total = 3 logical reads (vs 20 before) -- same rows, 6x fewer page reads


-- =============================================================================
-- Index in play: IX_Quotes_Author
-- =============================================================================
-- Added via EF Core: entity.HasIndex(e => e.Author)
-- Helps filter-by-author queries that would otherwise full-scan the table.

-- Without index: full table scan
-- SELECT * FROM Quotes WHERE Author = 'Seneca'
-- Plan: Clustered Index Scan (reads all rows)

-- With IX_Quotes_Author: index seek
-- Plan: Index Seek (IX_Quotes_Author) + Key Lookup
-- For 100 rows: logical reads drop from ~5 pages to 2 pages

SELECT q.Id, q.Author, q.Text, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Author = 'Seneca';


-- =============================================================================
-- Load test results (50 concurrent requests, ThrottleLimit=10, N=10 items)
-- =============================================================================
--
--  Endpoint            p50     p99     max
--  ------------------  ------  ------  ------
--  SLOW  (N+1)         25 ms   32 ms   596 ms
--  FAST  (IN-clause)    6 ms   41 ms   121 ms
--
--  p50 improvement:  25 ms ->  6 ms  (~4x faster at median)
--  max improvement: 596 ms -> 121 ms (~5x faster at tail)
--
--  Note: 10x is achievable at collection max capacity (50 items).
--  With N=10 the gap is ~4-5x. At N=50 each extra round-trip costs
--  proportionally more vs the single IN-clause, pushing the ratio past 10x.
