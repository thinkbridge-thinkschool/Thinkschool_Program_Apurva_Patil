using Microsoft.EntityFrameworkCore;
using QuotesDay7Task3.Data;

// ── Connection string ─────────────────────────────────────────────────────────
// Own database so Task3 is self-contained and doesn't disturb Task1/Task2 data.
const string connectionString =
    "Server=(localdb)\\mssqllocaldb;Database=QuotesDay7Task3;Trusted_Connection=True;";

var options = new DbContextOptionsBuilder<QuotesDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var ctx = new QuotesDbContext(options);

// Fresh schema + seed on every run
await ctx.Database.EnsureDeletedAsync();
await ctx.Database.EnsureCreatedAsync();

// ── Seed authors (same 10 as Task1) ──────────────────────────────────────────
var marcus    = new Author { Name = "Marcus Aurelius", Bio = "Roman Emperor and Stoic philosopher."            };
var seneca    = new Author { Name = "Seneca",          Bio = "Roman Stoic philosopher and statesman."          };
var epict     = new Author { Name = "Epictetus",       Bio = "Greek Stoic philosopher, born a slave."          };
var aristotle = new Author { Name = "Aristotle",       Bio = "Greek philosopher and polymath."                 };
var socrates  = new Author { Name = "Socrates",        Bio = "Classical Greek philosopher."                    };
var nietzsche = new Author { Name = "Nietzsche",       Bio = "German philosopher."                             };
var confucius = new Author { Name = "Confucius",       Bio = "Chinese philosopher."                            };
var buddha    = new Author { Name = "Buddha",          Bio = "Spiritual teacher."                              };
var plato     = new Author { Name = "Plato",           Bio = "Ancient Greek philosopher."                      };
var laozi     = new Author { Name = "Lao Tzu",         Bio = "Ancient Chinese philosopher — no quotes seeded." };

ctx.Authors.AddRange(marcus, seneca, epict, aristotle, socrates, nietzsche, confucius, buddha, plato, laozi);
await ctx.SaveChangesAsync();

var now = DateTimeOffset.UtcNow;

// ── Seed quotes (same texts as Task1) ────────────────────────────────────────
var q1  = new Quote { AuthorId = marcus.Id,    Text = "You have power over your mind, not outside events.",                                     CreatedAt = now.AddDays(-10) };
var q2  = new Quote { AuthorId = marcus.Id,    Text = "The impediment to action advances action. What stands in the way becomes the way.",      CreatedAt = now.AddDays(-5)  };
var q3  = new Quote { AuthorId = marcus.Id,    Text = "Waste no more time arguing about what a good man should be. Be one.",                    CreatedAt = now.AddDays(-1)  };
var q4  = new Quote { AuthorId = seneca.Id,    Text = "Luck is what happens when preparation meets opportunity.",                               CreatedAt = now.AddDays(-8)  };
var q5  = new Quote { AuthorId = seneca.Id,    Text = "We suffer more in imagination than in reality.",                                         CreatedAt = now.AddDays(-3)  };
var q6  = new Quote { AuthorId = epict.Id,     Text = "Make the best use of what is in your power.",                                            CreatedAt = now.AddDays(-6)  };
var q7  = new Quote { AuthorId = aristotle.Id, Text = "We are what we repeatedly do. Excellence is not an act but a habit.",                   CreatedAt = now.AddDays(-9)  };
var q8  = new Quote { AuthorId = aristotle.Id, Text = "The more you know, the more you know you don't know.",                                  CreatedAt = now.AddDays(-2)  };
var q9  = new Quote { AuthorId = socrates.Id,  Text = "The unexamined life is not worth living.",                                               CreatedAt = now.AddDays(-7)  };
var q10 = new Quote { AuthorId = socrates.Id,  Text = "I know that I know nothing.",                                                            CreatedAt = now.AddDays(-4)  };
var q11 = new Quote { AuthorId = nietzsche.Id, Text = "That which does not kill us makes us stronger.",                                         CreatedAt = now.AddDays(-12) };
var q12 = new Quote { AuthorId = confucius.Id, Text = "It does not matter how slowly you go as long as you do not stop.",                      CreatedAt = now.AddDays(-11) };
var q13 = new Quote { AuthorId = buddha.Id,    Text = "The mind is everything. What you think, you become.",                                    CreatedAt = now.AddDays(-13) };
var q14 = new Quote { AuthorId = plato.Id,     Text = "Wise men talk because they have something to say; fools, because they have to say something.", CreatedAt = now.AddDays(-14) };

ctx.Quotes.AddRange(q1, q2, q3, q4, q5, q6, q7, q8, q9, q10, q11, q12, q13, q14);
await ctx.SaveChangesAsync();

// ── Seed tags ─────────────────────────────────────────────────────────────────
var tWisdom     = new Tag { Name = "wisdom"      };
var tResilience = new Tag { Name = "resilience"  };
var tVirtue     = new Tag { Name = "virtue"      };
var tStoic      = new Tag { Name = "stoic"       };
var tExistence  = new Tag { Name = "existence"   };
var tMindful    = new Tag { Name = "mindfulness" };
var tStrength   = new Tag { Name = "strength"    };
var tGrowth     = new Tag { Name = "growth"      };

ctx.Tags.AddRange(tWisdom, tResilience, tVirtue, tStoic, tExistence, tMindful, tStrength, tGrowth);
await ctx.SaveChangesAsync();

// ── Seed QuoteTags ────────────────────────────────────────────────────────────
// Confucius (q12) and Buddha (q13) are intentionally left untagged so they
// appear as the EXCEPT result in Q1.
ctx.QuoteTags.AddRange(
    new QuoteTag { QuoteId = q1.Id,  TagId = tWisdom.Id     },  // Marcus
    new QuoteTag { QuoteId = q1.Id,  TagId = tStoic.Id      },
    new QuoteTag { QuoteId = q2.Id,  TagId = tResilience.Id },
    new QuoteTag { QuoteId = q2.Id,  TagId = tStoic.Id      },
    new QuoteTag { QuoteId = q3.Id,  TagId = tVirtue.Id     },
    new QuoteTag { QuoteId = q4.Id,  TagId = tWisdom.Id     },  // Seneca
    new QuoteTag { QuoteId = q4.Id,  TagId = tResilience.Id },
    new QuoteTag { QuoteId = q5.Id,  TagId = tMindful.Id    },
    new QuoteTag { QuoteId = q6.Id,  TagId = tStoic.Id      },  // Epictetus
    new QuoteTag { QuoteId = q7.Id,  TagId = tWisdom.Id     },  // Aristotle
    new QuoteTag { QuoteId = q7.Id,  TagId = tGrowth.Id     },
    new QuoteTag { QuoteId = q8.Id,  TagId = tWisdom.Id     },
    new QuoteTag { QuoteId = q9.Id,  TagId = tExistence.Id  },  // Socrates
    new QuoteTag { QuoteId = q9.Id,  TagId = tVirtue.Id     },
    new QuoteTag { QuoteId = q10.Id, TagId = tWisdom.Id     },
    new QuoteTag { QuoteId = q10.Id, TagId = tExistence.Id  },
    new QuoteTag { QuoteId = q11.Id, TagId = tStrength.Id   },  // Nietzsche
    new QuoteTag { QuoteId = q11.Id, TagId = tResilience.Id },
    new QuoteTag { QuoteId = q14.Id, TagId = tWisdom.Id     },  // Plato
    new QuoteTag { QuoteId = q14.Id, TagId = tVirtue.Id     }
);
await ctx.SaveChangesAsync();

// ── Seed AuthorCategories ─────────────────────────────────────────────────────
// Marcus is in BOTH sets → he will be the single INTERSECT hit in Q2.
// Confucius, Buddha, Lao Tzu have no category row.
ctx.AuthorCategories.AddRange(
    new AuthorCategory { AuthorId = marcus.Id,    Category = "classic" },
    new AuthorCategory { AuthorId = marcus.Id,    Category = "modern"  },
    new AuthorCategory { AuthorId = seneca.Id,    Category = "classic" },
    new AuthorCategory { AuthorId = epict.Id,     Category = "classic" },
    new AuthorCategory { AuthorId = aristotle.Id, Category = "modern"  },
    new AuthorCategory { AuthorId = socrates.Id,  Category = "classic" },
    new AuthorCategory { AuthorId = nietzsche.Id, Category = "modern"  },
    new AuthorCategory { AuthorId = plato.Id,     Category = "classic" }
);
await ctx.SaveChangesAsync();
Console.WriteLine("Database seeded.\n");

// ── Q1  EXCEPT ────────────────────────────────────────────────────────────────
// (all authors with quotes) EXCEPT (authors whose quotes have any tag)
// = authors who have written quotes but never applied a tag
const string q1Sql = """
    SELECT DISTINCT a.Id   AS AuthorId,
                    a.Name AS AuthorName
    FROM   Authors a
    JOIN   Quotes  q  ON  q.AuthorId = a.Id AND q.IsDeleted = 0

    EXCEPT

    SELECT DISTINCT a.Id   AS AuthorId,
                    a.Name AS AuthorName
    FROM   Authors   a
    JOIN   Quotes    q   ON  q.AuthorId = a.Id AND q.IsDeleted = 0
    JOIN   QuoteTags qt  ON  qt.QuoteId = q.Id
    """;

var untaggedAuthors = (await ctx.AuthorNameRows.FromSqlRaw(q1Sql).ToListAsync())
                          .OrderBy(r => r.AuthorName).ToList();

Console.WriteLine("Q1 — EXCEPT  — Authors with quotes but NO tags");
Console.WriteLine(new string('─', 52));
foreach (var r in untaggedAuthors)
    Console.WriteLine($"  [{r.AuthorId,2}]  {r.AuthorName}");
Console.WriteLine($"  ({untaggedAuthors.Count} row(s))\n");

// ── Q2  INTERSECT ─────────────────────────────────────────────────────────────
// (authors in 'classic') INTERSECT (authors in 'modern')
// = only authors present in both category sets
const string q2Sql = """
    SELECT a.Id   AS AuthorId,
           a.Name AS AuthorName
    FROM   Authors           a
    JOIN   AuthorCategories  ac ON ac.AuthorId = a.Id AND ac.Category = 'classic'

    INTERSECT

    SELECT a.Id   AS AuthorId,
           a.Name AS AuthorName
    FROM   Authors           a
    JOIN   AuthorCategories  ac ON ac.AuthorId = a.Id AND ac.Category = 'modern'
    """;

var dualCategoryAuthors = (await ctx.AuthorNameRows.FromSqlRaw(q2Sql).ToListAsync())
                              .OrderBy(r => r.AuthorName).ToList();

Console.WriteLine("Q2 — INTERSECT — Authors in both 'classic' AND 'modern'");
Console.WriteLine(new string('─', 52));
foreach (var r in dualCategoryAuthors)
    Console.WriteLine($"  [{r.AuthorId,2}]  {r.AuthorName}");
Console.WriteLine($"  ({dualCategoryAuthors.Count} row(s))\n");

// ── Q3  UNION ─────────────────────────────────────────────────────────────────
// (tags used by 'classic' authors) UNION (tags used by 'modern' authors)
// UNION deduplicates — tags shared by both categories appear exactly once
const string q3Sql = """
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
    JOIN   AuthorCategories  ac ON ac.AuthorId = q.AuthorId AND ac.Category = 'modern'
    """;

var allTags = (await ctx.TagNameRows.FromSqlRaw(q3Sql).ToListAsync())
                  .OrderBy(r => r.TagName).ToList();

Console.WriteLine("Q3 — UNION — Distinct tags across 'classic' ∪ 'modern'");
Console.WriteLine(new string('─', 52));
foreach (var r in allTags)
    Console.WriteLine($"  {r.TagName}");
Console.WriteLine($"  ({allTags.Count} tag(s))");
