-- Run this inside sqlite3 to verify seed data was loaded correctly
-- Usage: .\sqlite3.exe quotes.db
-- Then: .read verify-seed-data.sql

SELECT 'Quotes'          AS [Table], COUNT(*) AS [Rows] FROM Quotes
UNION ALL
SELECT 'Collections',    COUNT(*) FROM Collections
UNION ALL
SELECT 'CollectionItems', COUNT(*) FROM CollectionItem
UNION ALL
SELECT 'Users',          COUNT(*) FROM Users;
