-- SQLite EXPLAIN QUERY PLAN — run inside sqlite3.exe quotes.db
-- Shows whether SQLite uses a full table scan or an index for each query

-- 1. Author lookup BEFORE index (full table scan)
EXPLAIN QUERY PLAN
SELECT * FROM Quotes WHERE Author = 'Marcus Aurelius';
-- Expected: SCAN Quotes  (reads every row — slow)

-- 2. Author lookup AFTER adding IX_Quotes_Author index
EXPLAIN QUERY PLAN
SELECT * FROM Quotes WHERE Author = 'Marcus Aurelius';
-- Expected: SEARCH Quotes USING INDEX IX_Quotes_Author (Author=?)

-- 3. Fast endpoint IN-clause (uses primary key — always fast)
EXPLAIN QUERY PLAN
SELECT * FROM Quotes WHERE Id IN (1,2,3,4,5,6,7,8,9,10);
-- Expected: SEARCH Quotes USING INTEGER PRIMARY KEY (rowid=?)

-- 4. Collection load (slow endpoint — query 1 of 11)
EXPLAIN QUERY PLAN
SELECT * FROM Collections WHERE Id = 1;
-- Expected: SEARCH Collections USING INTEGER PRIMARY KEY (rowid=?)
