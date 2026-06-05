using Microsoft.EntityFrameworkCore;
using QuotesDay7Task2.Data;

// ── Connection string ─────────────────────────────────────────────────────────
// Reuses the same QuotesDay7 database seeded by Task1.
const string connectionString =
    "Server=(localdb)\\mssqllocaldb;Database=QuotesDay7;Trusted_Connection=True;";

// ── Build DbContext ───────────────────────────────────────────────────────────
var options = new DbContextOptionsBuilder<QuotesDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var ctx = new QuotesDbContext(options);

// EnsureCreatedAsync is a no-op when the database already exists (Task1 created it)
await ctx.Database.EnsureCreatedAsync();

if (!await ctx.Authors.AnyAsync())
{
    Console.WriteLine("No data found. Run Task1 first to seed the database.");
    return;
}

// ── Window-function query ─────────────────────────────────────────────────────
// Mirrors Queries/window_functions.sql exactly.
const string sql = """
    WITH WindowedQuotes AS (
        SELECT
            q.Id                                                                          AS QuoteId,
            q.AuthorId,
            q.Text,
            q.CreatedAt,
            CAST(ROW_NUMBER() OVER (PARTITION BY q.AuthorId ORDER BY q.CreatedAt) AS INT)              AS RowNum,
            CAST(RANK()       OVER (PARTITION BY q.AuthorId ORDER BY q.CreatedAt) AS INT)              AS Rnk,
            CAST(SUM(1)       OVER (PARTITION BY q.AuthorId ORDER BY q.CreatedAt
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS INT)              AS RunningTotal,
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
        DATEDIFF(day, wq.PrevQuoteAt, wq.CreatedAt)               AS DaysSincePrevious,
        wq.Text                                                   AS QuoteText
    FROM        WindowedQuotes  wq
    JOIN        Authors         a   ON  a.Id = wq.AuthorId
    ORDER BY    a.Name, wq.RowNum
    """;

var rows = await ctx.QuoteWindowRows
    .FromSqlRaw(sql)
    .ToListAsync();

// ── Print results ─────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine($"{"Author",-20} {"ID",4}  {"#",3}  {"Rank",4}  {"Running",7}  {"DaysGap",7}  Quote");
Console.WriteLine(new string('─', 90));

string? lastAuthor = null;

foreach (var r in rows)
{
    // Blank line between authors for readability
    if (lastAuthor is not null && lastAuthor != r.AuthorName)
        Console.WriteLine();

    var gap     = r.DaysSincePrevious.HasValue ? r.DaysSincePrevious.Value.ToString() : "NULL";
    var quote   = r.QuoteText.Length > 42 ? r.QuoteText[..42] + "…" : r.QuoteText;

    Console.WriteLine(
        $"{r.AuthorName,-20} {r.QuoteId,4}  {r.RowNum,3}  {r.Rnk,4}  {r.RunningTotal,7}  {gap,7}  {quote}");

    lastAuthor = r.AuthorName;
}

Console.WriteLine();
Console.WriteLine($"Total rows: {rows.Count}");
