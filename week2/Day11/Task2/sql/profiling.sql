-- =============================================================================
-- Day 11 Task 2 — Before / After Query Plans (SQL Server)
-- =============================================================================
-- RUN THIS IN SSMS. Copy-paste the full output into Solution.md.
--
-- Prereq: POST /api/dev/seed   (creates 100 quotes, Collection-1 with 100 items)
-- Verify: SELECT COUNT(*) FROM CollectionItems WHERE CollectionId = 1  → expect 100
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- SECTION 1: STATISTICS IO — logical reads before vs after
-- Run each block separately to see clean IO output per query.
-- ─────────────────────────────────────────────────────────────────────────────

SET STATISTICS IO ON;
SET STATISTICS TIME ON;

-- ── BEFORE: individual quote fetch (the N+1 loop does this 50 times) ────────
-- This is what EF Core generates inside the foreach loop, once per item:
PRINT '=== BEFORE: single-row quote fetch (repeated 50x in N+1 loop) ===';
SELECT TOP(1) q.Id, q.Author, q.Text, q.IsDeleted, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Id = 1;

-- ── AFTER: single IN-clause fetch for all 100 quotes ────────────────────────
PRINT '=== AFTER: single IN-clause for all 100 quote IDs ===';
SELECT q.Id, q.Author, q.Text, q.IsDeleted, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Id IN (
    1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,
    21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,
    41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,
    61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,
    81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100
);

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;

-- ─────────────────────────────────────────────────────────────────────────────
-- SECTION 2: Execution plan XML — run each with Actual Execution Plan ON
-- ─────────────────────────────────────────────────────────────────────────────

-- Query 2a — single row lookup (N+1 loop body)
SELECT TOP(1) q.Id, q.Author, q.Text, q.IsDeleted, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Id = 1;

-- Query 2b — IN-clause for all 100 items
SELECT q.Id, q.Author, q.Text, q.IsDeleted, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Id IN (
    1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,
    21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,
    41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,
    61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,
    81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100
);

-- ─────────────────────────────────────────────────────────────────────────────
-- SECTION 3: Covering index — verify no Key Lookup on author-filtered query
-- ─────────────────────────────────────────────────────────────────────────────

SET STATISTICS IO ON;

PRINT '=== Author-filtered query — should use IX_Quotes_Author (no key lookup) ===';
SELECT q.Author, q.Text, q.CreatedAt
FROM   Quotes AS q
WHERE  q.Author = 'Seneca';

SET STATISTICS IO OFF;

-- Expected output after covering index is applied:
--   Table 'Quotes'. Scan count 1, logical reads 2  (index leaf only, no key lookup)
-- Before covering index:
--   Table 'Quotes'. Scan count 1, logical reads N  (index seek + key lookup per row)

-- ─────────────────────────────────────────────────────────────────────────────
-- SECTION 4: Verify seed is correct before running k6
-- ─────────────────────────────────────────────────────────────────────────────

SELECT c.Id, c.Name, COUNT(i.QuoteId) AS ItemCount
FROM   Collections c
JOIN   CollectionItem i ON i.CollectionId = c.Id
GROUP  BY c.Id, c.Name;
-- Collection-1 must show ItemCount = 100

SELECT COUNT(*) AS TotalQuotes FROM Quotes;
-- Must be 100 (5 authors × 20 quotes)
