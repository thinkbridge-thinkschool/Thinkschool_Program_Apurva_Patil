-- Day 10 Task 1 — EFCoreDemo database setup
-- Creates EFCoreDemoDay10 and seeds 10 000 Products.
-- Run via: sqlcmd -S "(localdb)\mssqllocaldb" -E -i DBSetup.sql
--
-- Uses a cross-join tally table to avoid the default MAXRECURSION 100
-- limit that would prevent a recursive CTE from reaching 10 000 rows.

USE master;
GO

IF DB_ID('EFCoreDemoDay10') IS NULL
    CREATE DATABASE EFCoreDemoDay10;
GO

USE EFCoreDemoDay10;
GO

IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id    INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Name  NVARCHAR(100) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        Stock INT           NOT NULL
    );
END
GO

-- Only seed when the table is empty (idempotent re-runs).
IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    -- Tally: cross-join two 100-row sets gives 10 000 combinations.
    WITH T1  AS (SELECT 1 n UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1
                 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1
                 UNION ALL SELECT 1 UNION ALL SELECT 1),     -- 10 rows
         T2  AS (SELECT 1 n FROM T1 a CROSS JOIN T1 b),      -- 100 rows
         T3  AS (SELECT 1 n FROM T2 a CROSS JOIN T2 b),      -- 10 000 rows
         Seq AS (SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i FROM T3)
    INSERT INTO dbo.Products (Name, Price, Stock)
    SELECT
        'Product-' + CAST(i AS NVARCHAR(10)),
        CAST(1 + (i % 999) AS DECIMAL(18,2)),
        CAST(i % 500 AS INT)
    FROM Seq
    WHERE i <= 10000;

    PRINT CAST(@@ROWCOUNT AS NVARCHAR) + ' rows inserted.';
END
ELSE
    PRINT 'Products table already seeded — skipped.';
GO
