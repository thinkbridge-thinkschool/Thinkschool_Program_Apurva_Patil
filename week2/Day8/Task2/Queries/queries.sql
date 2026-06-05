-- ============================================================
-- STEP A: Key Lookup — NCI on CustomerId, no INCLUDE
-- ============================================================
-- The NCI leaf stores only: CustomerId (key) + OrderId (row locator).
-- The query also needs OrderDate and TotalAmount, which are NOT in
-- the index. SQL Server must do an extra Key Lookup into the
-- clustered index for every matching row to fetch those columns.
-- ============================================================

-- Reset: drop either index if it already exists so this script is rerunnable
DROP INDEX IF EXISTS IX_Orders_CustomerId_Covering    ON Orders;
DROP INDEX IF EXISTS IX_Orders_CustomerId_NoInclude   ON Orders;

CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_NoInclude
ON Orders(CustomerId);

SET STATISTICS IO ON;
SET STATISTICS PROFILE ON;

-- In SSMS: enable "Include Actual Execution Plan" (Ctrl+M) first.
-- Plan will show:  Index Seek  →  Key Lookup  →  Nested Loops
SELECT OrderId, OrderDate, TotalAmount
FROM Orders
WHERE CustomerId = 1234;

SET STATISTICS PROFILE OFF;
SET STATISTICS IO OFF;


-- ============================================================
-- STEP B: Covering Index — same NCI with INCLUDE
-- ============================================================
-- Adding OrderDate and TotalAmount to INCLUDE embeds them in
-- every NCI leaf page. The query is now served entirely from
-- the index — no trip back to the clustered index needed.
-- ============================================================

DROP INDEX IF EXISTS IX_Orders_CustomerId_NoInclude ON Orders;

CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Covering
ON Orders(CustomerId)
INCLUDE (OrderDate, TotalAmount);

SET STATISTICS IO ON;
SET STATISTICS PROFILE ON;

-- Same query — plan will show only: Index Seek (no Key Lookup)
SELECT OrderId, OrderDate, TotalAmount
FROM Orders
WHERE CustomerId = 1234;

SET STATISTICS PROFILE OFF;
SET STATISTICS IO OFF;


-- ============================================================
-- Cleanup
-- ============================================================
DROP INDEX IF EXISTS IX_Orders_CustomerId_Covering ON Orders;
