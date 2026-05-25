-- =============================================================================
-- Day 7 — Window Functions
-- Goal : per author, each quote with a running count, rank, running total,
--        and gap in days since the author's previous quote (LAG).
--
-- Window functions demonstrated
--   ROW_NUMBER()  — sequential row number per author, no tie-sharing
--   RANK()        — same ordering; ties share a rank (would differ if two
--                   quotes had identical CreatedAt)
--   SUM(1) OVER   — explicit running total (shows the SUM OVER pattern)
--   LAG()         — previous CreatedAt per author → DATEDIFF gives day gap
-- =============================================================================

-- ── Approach ─────────────────────────────────────────────────────────────────
-- CTE WindowedQuotes  – applies all four window functions in one pass;
--                       LAG captures the previous row's CreatedAt so the
--                       outer SELECT can call DATEDIFF without a self-join.
-- Final SELECT        – joins Authors for the name, computes DaysSincePrevious,
--                       orders by author then chronological quote position.
-- =============================================================================

WITH WindowedQuotes AS (
    SELECT
        q.Id                                                                          AS QuoteId,
        q.AuthorId,
        q.Text,
        q.CreatedAt,

        -- Sequential position of this quote within the author's timeline
        CAST(ROW_NUMBER() OVER (PARTITION BY q.AuthorId ORDER BY q.CreatedAt) AS INT)              AS RowNum,

        -- Rank — identical to ROW_NUMBER when no ties; shown to demonstrate
        -- that RANK shares numbers on ties while ROW_NUMBER never does
        CAST(RANK()       OVER (PARTITION BY q.AuthorId ORDER BY q.CreatedAt) AS INT)              AS Rnk,

        -- Running total of quotes seen so far for this author
        -- SUM(1) OVER (...) is the explicit form of the running-count pattern
        CAST(SUM(1)       OVER (PARTITION BY q.AuthorId ORDER BY q.CreatedAt
                           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS INT)               AS RunningTotal,

        -- Previous quote's timestamp for this author (NULL for the first row)
        LAG(q.CreatedAt) OVER (PARTITION BY q.AuthorId ORDER BY q.CreatedAt)          AS PrevQuoteAt

    FROM   Quotes q
    WHERE  q.IsDeleted = 0
)

SELECT
    a.Name                                                    AS AuthorName,
    wq.QuoteId,
    wq.RowNum,
    wq.Rnk,
    wq.RunningTotal,
    -- NULL for first quote; positive integer for every subsequent one
    DATEDIFF(day, wq.PrevQuoteAt, wq.CreatedAt)               AS DaysSincePrevious,
    wq.Text                                                   AS QuoteText
FROM        WindowedQuotes  wq
JOIN        Authors         a   ON  a.Id = wq.AuthorId
ORDER BY    a.Name, wq.RowNum;

-- =============================================================================
-- Expected sample output (seeded data from Task1, dates relative to seed day)
--
--  AuthorName       QuoteId  RowNum  Rnk  Running  DaysGap  QuoteText
--  ───────────────  ───────  ──────  ───  ───────  ───────  ──────────────────
--  Epictetus              6       1    1        1     NULL   Make the best use…
--  Marcus Aurelius        1       1    1        1     NULL   You have power ov…
--  Marcus Aurelius        2       2    2        2        5   The impediment to…
--  Marcus Aurelius        3       3    3        3        4   Waste no more tim…
--  Seneca                 4       1    1        1     NULL   Luck is what happ…
--  Seneca                 5       2    2        2        5   We suffer more in…
-- =============================================================================
