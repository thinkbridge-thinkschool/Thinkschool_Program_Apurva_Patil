-- Fix: Add index on Quotes.Author column
-- Without this index, every WHERE Author = ? query does a full table scan (SCAN Quotes)
-- After this index, SQLite uses SEARCH Quotes USING INDEX IX_Quotes_Author (Author=?)

CREATE INDEX IF NOT EXISTS "IX_Quotes_Author" ON "Quotes" ("Author");