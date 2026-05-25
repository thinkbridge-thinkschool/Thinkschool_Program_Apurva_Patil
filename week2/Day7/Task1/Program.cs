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

// Drop and recreate to ensure seed data is always fresh
await ctx.Database.EnsureDeletedAsync();
await ctx.Database.EnsureCreatedAsync();

// ── Seed 10 authors ───────────────────────────────────────────────────────────
var marcus    = new Author { Name = "Marcus Aurelius",  Bio = "Roman Emperor and Stoic philosopher." };
var seneca    = new Author { Name = "Seneca",           Bio = "Roman Stoic philosopher and statesman." };
var epict     = new Author { Name = "Epictetus",        Bio = "Greek Stoic philosopher, born a slave." };
var aristotle = new Author { Name = "Aristotle",        Bio = "Greek philosopher and polymath, student of Plato." };
var socrates  = new Author { Name = "Socrates",         Bio = "Classical Greek philosopher and founder of Western philosophy." };
var nietzsche = new Author { Name = "Nietzsche",        Bio = "German philosopher known for challenging traditional morality." };
var confucius = new Author { Name = "Confucius",        Bio = "Chinese philosopher and founder of Confucianism." };
var buddha    = new Author { Name = "Buddha",           Bio = "Spiritual teacher and founder of Buddhism." };
var plato     = new Author { Name = "Plato",            Bio = "Ancient Greek philosopher, student of Socrates." };
var laozi     = new Author { Name = "Lao Tzu",          Bio = "Ancient Chinese philosopher and founder of Taoism. No quotes seeded — tests LEFT JOIN." };

ctx.Authors.AddRange(marcus, seneca, epict, aristotle, socrates, nietzsche, confucius, buddha, plato, laozi);
await ctx.SaveChangesAsync();

var now = DateTimeOffset.UtcNow;

ctx.Quotes.AddRange(
    // Marcus — 3 quotes
    new Quote { AuthorId = marcus.Id,    Text = "You have power over your mind, not outside events.",                                    CreatedAt = now.AddDays(-10) },
    new Quote { AuthorId = marcus.Id,    Text = "The impediment to action advances action. What stands in the way becomes the way.",     CreatedAt = now.AddDays(-5)  },
    new Quote { AuthorId = marcus.Id,    Text = "Waste no more time arguing about what a good man should be. Be one.",                   CreatedAt = now.AddDays(-1)  },

    // Seneca — 2 quotes
    new Quote { AuthorId = seneca.Id,    Text = "Luck is what happens when preparation meets opportunity.",                              CreatedAt = now.AddDays(-8)  },
    new Quote { AuthorId = seneca.Id,    Text = "We suffer more in imagination than in reality.",                                        CreatedAt = now.AddDays(-3)  },

    // Epictetus — 1 quote
    new Quote { AuthorId = epict.Id,     Text = "Make the best use of what is in your power.",                                           CreatedAt = now.AddDays(-6)  },

    // Aristotle — 2 quotes
    new Quote { AuthorId = aristotle.Id, Text = "We are what we repeatedly do. Excellence is not an act but a habit.",                  CreatedAt = now.AddDays(-9)  },
    new Quote { AuthorId = aristotle.Id, Text = "The more you know, the more you know you don't know.",                                 CreatedAt = now.AddDays(-2)  },

    // Socrates — 2 quotes
    new Quote { AuthorId = socrates.Id,  Text = "The unexamined life is not worth living.",                                              CreatedAt = now.AddDays(-7)  },
    new Quote { AuthorId = socrates.Id,  Text = "I know that I know nothing.",                                                           CreatedAt = now.AddDays(-4)  },

    // Nietzsche — 1 quote
    new Quote { AuthorId = nietzsche.Id, Text = "That which does not kill us makes us stronger.",                                        CreatedAt = now.AddDays(-12) },

    // Confucius — 1 quote
    new Quote { AuthorId = confucius.Id, Text = "It does not matter how slowly you go as long as you do not stop.",                     CreatedAt = now.AddDays(-11) },

    // Buddha — 1 quote
    new Quote { AuthorId = buddha.Id,    Text = "The mind is everything. What you think, you become.",                                   CreatedAt = now.AddDays(-13) },

    // Plato — 1 quote
    new Quote { AuthorId = plato.Id,     Text = "Wise men talk because they have something to say; fools, because they have to say something.", CreatedAt = now.AddDays(-14) },

    // Soft-deleted quote — must NOT appear in results
    new Quote { AuthorId = marcus.Id,    Text = "This quote was deleted and should be invisible.",                                       CreatedAt = now, IsDeleted = true }
);

await ctx.SaveChangesAsync();
Console.WriteLine("Database seeded with 10 authors.\n");

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
