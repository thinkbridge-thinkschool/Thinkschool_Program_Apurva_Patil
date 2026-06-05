-- =============================================================================
-- fix-add-index.sql  —  Upgrade narrow index to covering index (SQL Server)
-- =============================================================================
-- Problem: IX_Quotes_Author existed as a narrow index (key column only).
-- When SQL Server uses it for a query that needs Text or CreatedAt too, it
-- must do a Key Lookup back to the clustered index for every matching row.
-- A covering index includes those columns so the query is satisfied entirely
-- from the index — no key lookup required.
-- =============================================================================


-- BEFORE: narrow index (only Author column in the index)
-- SQL Server execution plan shows two operations per matching row:
--   1. Index Seek  on IX_Quotes_Author  → finds matching row IDs
--   2. Key Lookup  on PK (Clustered)    → fetches Text + CreatedAt per row
-- Under load this doubles the I/O for every author-filtered query.


-- Step 1: drop the old narrow index
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name      = N'IX_Quotes_Author'
      AND  object_id = OBJECT_ID(N'[dbo].[Quotes]'))
    DROP INDEX [IX_Quotes_Author] ON [dbo].[Quotes];


-- Step 2: recreate as a covering index
-- Author is the seek key; Text + CreatedAt are included so SQL Server never
-- needs to go back to the clustered index for these columns.
CREATE NONCLUSTERED INDEX [IX_Quotes_Author]
    ON  [dbo].[Quotes] ([Author] ASC)
    INCLUDE ([Text], [CreatedAt]);


-- =============================================================================
-- How EF Core generates this (see AddCoveringAuthorIndex migration):
-- =============================================================================
--   migrationBuilder.CreateIndex(
--       name: "IX_Quotes_Author",
--       table: "Quotes",
--       column: "Author")
--       .Annotation("SqlServer:Include", new[] { "Text", "CreatedAt" });


-- =============================================================================
-- Verify: compare logical reads before vs after with STATISTICS IO
-- =============================================================================
SET STATISTICS IO ON;

SELECT Author, Text, CreatedAt
FROM   Quotes
WHERE  Author = 'Seneca';

-- WITHOUT covering index:
--   Index Seek (IX_Quotes_Author)  +  Key Lookup per row  =  high logical reads
--
-- WITH covering index:
--   Index Seek (IX_Quotes_Author) only, all columns in leaf page  =  low logical reads

SET STATISTICS IO OFF;
