-- ============================================
-- SECTION 1: BEFORE INDEXES (Heap Scan)
-- ============================================
SET STATISTICS IO ON;
SET STATISTICS PROFILE ON;

-- Q1: Single row lookup
SELECT * FROM Orders WHERE OrderId = 50000;

-- Q2: Customer order history
SELECT OrderId, OrderDate, TotalAmount FROM Orders WHERE CustomerId = 1234;

-- Q3: Date range query
SELECT * FROM Orders WHERE OrderDate BETWEEN '2025-01-01' AND '2025-03-31';

-- Q4: INSERT - baseline write cost
INSERT INTO Orders VALUES (100001, 9999, '2026-01-15', 'Pending', 250.00, 'North');

SET STATISTICS PROFILE OFF;
SET STATISTICS IO OFF;

-- ============================================
-- SECTION 2: CREATE INDEXES
-- ============================================

CREATE CLUSTERED INDEX CIX_Orders_OrderId
ON Orders(OrderId);

CREATE NONCLUSTERED INDEX IX_Orders_CustomerId
ON Orders(CustomerId)
INCLUDE (OrderDate, TotalAmount);

CREATE NONCLUSTERED INDEX IX_Orders_OrderDate
ON Orders(OrderDate)
INCLUDE (CustomerId, TotalAmount);

-- ============================================
-- SECTION 3: AFTER INDEXES
-- ============================================
SET STATISTICS IO ON;
SET STATISTICS PROFILE ON;

-- Q1: Single row lookup
SELECT * FROM Orders WHERE OrderId = 50000;

-- Q2: Customer order history
SELECT OrderId, OrderDate, TotalAmount FROM Orders WHERE CustomerId = 1234;

-- Q3: Date range query
SELECT * FROM Orders WHERE OrderDate BETWEEN '2025-01-01' AND '2025-03-31';

-- Q4: INSERT - write cost with indexes
INSERT INTO Orders VALUES (100002, 8888, '2026-02-20', 'Shipped', 500.00, 'South');

SET STATISTICS PROFILE OFF;
SET STATISTICS IO OFF;
