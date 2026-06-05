-- =============================================================================
-- Day 7 Task 3 — Set Operations  (UNION / INTERSECT / EXCEPT)
--
-- Schema additions over Task1/Task2:
--   Tags             (Id, Name)
--   QuoteTags        (QuoteId FK, TagId FK)        many-to-many bridge
--   AuthorCategories (AuthorId FK, Category)       an author can be in multiple sets
--
-- Seed intent:
--   • Confucius and Buddha have quotes but NO tags   → appear only in Q1
--   • Marcus Aurelius is in both 'classic' and 'modern' → appears in Q2
--   • 'classic' ∪ 'modern' covers all 8 tags         → Q3 returns all 8
-- =============================================================================


-- ── Q1  EXCEPT ────────────────────────────────────────────────────────────────
-- Business question: Which authors have written quotes but tagged none of them?
-- Operator: EXCEPT removes from the left set every author whose quotes
--           carry at least one tag.  What remains are authors with quotes
--           but zero tag coverage.

SELECT DISTINCT a.Id   AS AuthorId,
                a.Name AS AuthorName
FROM   Authors a
JOIN   Quotes  q  ON  q.AuthorId = a.Id AND q.IsDeleted = 0

EXCEPT

SELECT DISTINCT a.Id   AS AuthorId,
                a.Name AS AuthorName
FROM   Authors   a
JOIN   Quotes    q   ON  q.AuthorId = a.Id AND q.IsDeleted = 0
JOIN   QuoteTags qt  ON  qt.QuoteId = q.Id;

-- Expected: Confucius, Buddha


-- ── Q2  INTERSECT ─────────────────────────────────────────────────────────────
-- Business question: Which authors belong to both the 'classic' and 'modern' sets?
-- Operator: INTERSECT keeps only rows that appear in BOTH sub-results.
--           Since AuthorCategories has one row per (author, category), an author
--           in both sets produces one row in each branch → INTERSECT finds them.

SELECT a.Id   AS AuthorId,
       a.Name AS AuthorName
FROM   Authors           a
JOIN   AuthorCategories  ac ON ac.AuthorId = a.Id AND ac.Category = 'classic'

INTERSECT

SELECT a.Id   AS AuthorId,
       a.Name AS AuthorName
FROM   Authors           a
JOIN   AuthorCategories  ac ON ac.AuthorId = a.Id AND ac.Category = 'modern';

-- Expected: Marcus Aurelius


-- ── Q3  UNION ─────────────────────────────────────────────────────────────────
-- Business question: What is the combined distinct tag vocabulary used by
--                    'classic' and 'modern' authors?
-- Operator: UNION (not UNION ALL) deduplicates automatically.
--           Tags shared by both categories appear once in the final result.

SELECT DISTINCT t.Name AS TagName
FROM   Tags              t
JOIN   QuoteTags         qt ON qt.TagId    = t.Id
JOIN   Quotes            q  ON q.Id        = qt.QuoteId AND q.IsDeleted = 0
JOIN   AuthorCategories  ac ON ac.AuthorId = q.AuthorId AND ac.Category = 'classic'

UNION

SELECT DISTINCT t.Name AS TagName
FROM   Tags              t
JOIN   QuoteTags         qt ON qt.TagId    = t.Id
JOIN   Quotes            q  ON q.Id        = qt.QuoteId AND q.IsDeleted = 0
JOIN   AuthorCategories  ac ON ac.AuthorId = q.AuthorId AND ac.Category = 'modern';

-- Expected: existence, growth, mindfulness, resilience, stoic, strength, virtue, wisdom
-- (8 tags — UNION deduplicates shared tags like wisdom, resilience, stoic)
-- =============================================================================
