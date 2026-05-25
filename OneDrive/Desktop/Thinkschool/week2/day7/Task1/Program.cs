using Microsoft.EntityFrameworkCore;
using QuotesDay7.Data;

// ── Connection string ─────────────────────────────────────────────────────────
// Uses SQL Server LocalDB (ships with Visual Studio on Windows).
// Change this to your Azure SQL or full SQL Server instance if preferred.
const string connectionString =
    "Server=(localdb)\\mssqllocaldb;Database=QuotesDay7;Trusted_Connection=True;";

// ── Build DbContext ───────────────────────────────────────────────────────────
var options = new DbContextOptionsBuilder<QuotesDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var ctx = new QuotesDbContext(options);

// Creates the database and tables if they don't exist yet (no migrations needed)
await ctx.Database.EnsureCreatedAsync();

// ── Seed sample data (only on first run) ─────────────────────────────────────
if (!await ctx.Authors.AnyAsync())
{
    var marcus  = new Author { Name = "Marcus Aurelius",  Bio = "Roman Emperor and Stoic philosopher." };
    var seneca  = new Author { Name = "Seneca",           Bio = "Roman Stoic philosopher and statesman." };
    var epict   = new Author { Name = "Epictetus",        Bio = "Greek Stoic philosopher, born a slave." };
    var noQuote = new Author { Name = "Plato",            Bio = "Author with no quotes yet — tests LEFT JOIN." };

    ctx.Authors.AddRange(marcus, seneca, epict, noQuote);
    await ctx.SaveChangesAsync();

    var now = DateTimeOffset.UtcNow;

    ctx.Quotes.AddRange(
        // Marcus — 3 quotes, different timestamps
        new Quote { AuthorId = marcus.Id, Text = "You have power over your mind, not outside events.",            CreatedAt = now.AddDays(-10) },
        new Quote { AuthorId = marcus.Id, Text = "The impediment to action advances action. What stands in the way becomes the way.", CreatedAt = now.AddDays(-5) },
        new Quote { AuthorId = marcus.Id, Text = "Waste no more time arguing about what a good man should be. Be one.",              CreatedAt = now.AddDays(-1) },

        // Seneca — 2 quotes
        new Quote { AuthorId = seneca.Id, Text = "Luck is what happens when preparation meets opportunity.", CreatedAt = now.AddDays(-8) },
        new Quote { AuthorId = seneca.Id, Text = "We suffer more in imagination than in reality.",           CreatedAt = now.AddDays(-3) },

        // Epictetus — 1 quote
        new Quote { AuthorId = epict.Id,  Text = "Make the best use of what is in your power.", CreatedAt = now.AddDays(-6) },

        // Soft-deleted quote — must NOT appear in results
        new Quote { AuthorId = marcus.Id, Text = "This quote was deleted and should be invisible.", CreatedAt = now, IsDeleted = true }
    );

    await ctx.SaveChangesAsync();
    Console.WriteLine("Database seeded.\n");
}

// ── The CTE query ─────────────────────────────────────────────────────────────
// Mirrors Queries/author_summary.sql exactly.
// Two CTEs → no correlated subquery in SELECT.
const string sql = """
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
    ORDER BY    COALESCE(s.QuoteCount, 0) DESC
    """;

var results = await ctx.AuthorSummaries
    .FromSqlRaw(sql)
    .ToListAsync();

// ── Print results ─────────────────────────────────────────────────────────────
Console.WriteLine($"{"Author",-20} {"Quotes",6}  Most Recent Quote");
Console.WriteLine(new string('-', 80));

foreach (var row in results)
{
    var quote = row.MostRecentQuoteText ?? "(no quotes)";
    var truncated = quote.Length > 50 ? quote[..50] + "…" : quote;
    Console.WriteLine($"{row.AuthorName,-20} {row.QuoteCount,6}  {truncated}");
}
