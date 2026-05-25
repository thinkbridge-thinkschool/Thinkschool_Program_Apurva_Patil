-- =============================================================================
-- Day 7 — Joins and CTEs at depth
-- Goal : each Author with their quote count and most-recent quote TEXT,
--        in one statement, using CTEs — no correlated subquery in SELECT.
-- =============================================================================

-- ── Approach ─────────────────────────────────────────────────────────────────
-- CTE 1  AuthorStats   – aggregate COUNT + MAX(CreatedAt) per author
-- CTE 2  LatestQuote   – join back to Quotes on (AuthorId, CreatedAt = LatestAt)
--                        to retrieve the text without touching SELECT's subquery
-- Final  LEFT JOIN both CTEs to Authors so authors with 0 quotes still appear
-- =============================================================================

WITH AuthorStats AS (
    SELECT
        AuthorId,
        COUNT(*)        AS QuoteCount,
        MAX(CreatedAt)  AS LatestAt
    FROM   Quotes
    WHERE  IsDeleted = 0
    GROUP  BY AuthorId
),

LatestQuote AS (
    -- INNER JOIN on both AuthorId and the exact max timestamp picks the row
    -- whose text we want — no correlated subquery needed in the outer SELECT
    SELECT
        q.AuthorId,
        q.Text AS MostRecentQuoteText
    FROM   Quotes       q
    JOIN   AuthorStats  s  ON  q.AuthorId  = s.AuthorId
                           AND q.CreatedAt = s.LatestAt
                           AND q.IsDeleted = 0
)

SELECT
    a.Id                            AS AuthorId,
    a.Name                          AS AuthorName,
    COALESCE(s.QuoteCount, 0)       AS QuoteCount,
    lq.MostRecentQuoteText
FROM        Authors      a
LEFT JOIN   AuthorStats  s   ON  a.Id = s.AuthorId
LEFT JOIN   LatestQuote  lq  ON  a.Id = lq.AuthorId
ORDER BY    COALESCE(s.QuoteCount, 0) DESC;

-- =============================================================================
-- NOTE — tie-breaking
-- If two quotes for the same author share an identical CreatedAt timestamp,
-- LatestQuote returns both rows and the outer LEFT JOIN fans out (duplicate
-- author rows). For production, replace the two CTEs above with a single
-- window-function CTE:
--
-- WITH RankedQuotes AS (
--     SELECT
--         AuthorId,
--         Text,
--         COUNT(*) OVER (PARTITION BY AuthorId)          AS QuoteCount,
--         ROW_NUMBER() OVER (PARTITION BY AuthorId
--                            ORDER BY CreatedAt DESC)    AS rn
--     FROM   Quotes
--     WHERE  IsDeleted = 0
-- )
-- SELECT
--     a.Id                         AS AuthorId,
--     a.Name                       AS AuthorName,
--     COALESCE(rq.QuoteCount, 0)   AS QuoteCount,
--     rq.Text                      AS MostRecentQuoteText
-- FROM        Authors       a
-- LEFT JOIN   RankedQuotes  rq  ON  a.Id = rq.AuthorId
--                               AND rq.rn = 1
-- ORDER BY    COALESCE(rq.QuoteCount, 0) DESC;
-- =============================================================================
