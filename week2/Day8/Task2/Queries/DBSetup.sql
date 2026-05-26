-- ============================================================
-- DBSetup.sql  —  Orders table with clustered index
-- ============================================================

DROP TABLE IF EXISTS Orders;

CREATE TABLE Orders (
    OrderId     INT            NOT NULL,
    CustomerId  INT            NOT NULL,
    OrderDate   DATE           NOT NULL,
    Status      VARCHAR(20)    NOT NULL,
    TotalAmount DECIMAL(10,2)  NOT NULL,
    Region      VARCHAR(50)    NOT NULL
);

-- Populate 100,000 rows
WITH Numbers AS (
    SELECT TOP 100000
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.objects a
    CROSS JOIN sys.objects b
    CROSS JOIN sys.objects c
)
INSERT INTO Orders (OrderId, CustomerId, OrderDate, Status, TotalAmount, Region)
SELECT
    n,
    (ABS(CHECKSUM(NEWID())) % 5000) + 1,
    DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 1095), '2026-01-01'),
    CASE (ABS(CHECKSUM(NEWID())) % 4)
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Shipped'
        WHEN 2 THEN 'Delivered'
        ELSE        'Cancelled'
    END,
    CAST((ABS(CHECKSUM(NEWID())) % 10000) + 1 AS DECIMAL(10,2)),
    CASE (ABS(CHECKSUM(NEWID())) % 5)
        WHEN 0 THEN 'North'
        WHEN 1 THEN 'South'
        WHEN 2 THEN 'East'
        WHEN 3 THEN 'West'
        ELSE        'Central'
    END
FROM Numbers;

-- Clustered index physically orders the table by OrderId.
-- This is required for a key lookup to exist later:
-- the NCI row locator points into this clustered structure.
CREATE CLUSTERED INDEX CIX_Orders_OrderId
ON Orders(OrderId);

SELECT COUNT(*) AS TotalRows FROM Orders;
