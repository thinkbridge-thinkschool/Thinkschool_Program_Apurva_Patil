using QueryTranslationDemo.Dtos;

namespace QueryTranslationDemo;

public static class ProjectionDemo
{
    // ── Demo: full entity vs .Select() projection — SQL column diff ───────────
    // Loading a full entity tells EF to SELECT every mapped column.
    // A .Select(p => new Dto { ... }) projection pushes a narrower column list
    // into the SQL: EF emits only the columns the DTO constructor references.
    //
    // WHY this matters:
    //   For wide tables (20+ columns, large NVARCHAR blobs) the bandwidth and
    //   allocation difference is measurable.  A covering index on only the
    //   projected columns also becomes possible, letting SQL Server satisfy the
    //   query from the index alone (no key lookup).
    //
    // RULE: project to a DTO on every read-only path that does not need all
    //       columns.  The logged SQL tells you exactly what you saved.
    public static void Run()
    {
        Console.WriteLine("── Full entity vs projection — SQL column diff ───────────────────────────");

        // ── BEFORE: full entity — EF selects all 4 columns ───────────────────
        Console.WriteLine("   [BEFORE] full entity: ctx.Products.Take(3).ToList()");
        Console.WriteLine("   Expected SQL columns: Id, Name, Price, Stock");
        Console.WriteLine();
        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var fullEntities = ctx.Products.Take(3).ToList();
            Console.WriteLine($"   → {fullEntities.Count} Product objects — 4 columns each");
        }
        Console.WriteLine();

        // ── AFTER: projection — EF selects only 2 columns ────────────────────
        Console.WriteLine("   [AFTER] projection to ProductSummaryDto");
        Console.WriteLine("   Expected SQL columns: Id, Name  (Price + Stock absent)");
        Console.WriteLine();
        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var dtos = ctx.Products
                .Select(p => new ProductSummaryDto { Id = p.Id, Name = p.Name })
                .Take(3)
                .ToList();
            Console.WriteLine($"   → {dtos.Count} ProductSummaryDto objects — 2 columns each");
            Console.WriteLine("   ↑ Price and Stock are absent from the SQL above.");
            Console.WriteLine("     They were never fetched, never allocated, never sent over the wire.");
        }
        Console.WriteLine();
    }

    // ── Demo: WHERE + projection — filter and column list in one SQL ──────────
    // Combining .Where() before .Select() shows that EF pushes both the
    // predicate (WHERE clause) and the column list (SELECT list) into a single
    // SQL statement — no extra round-trip, no extra columns.
    public static void RunFiltered()
    {
        Console.WriteLine("── WHERE + projection — one SQL, narrow result ───────────────────────────");
        Console.WriteLine("   ctx.Products.Where(p => p.Price > 900m).Select(p => new Dto{...}).ToList()");
        Console.WriteLine();
        using var ctx = AppDbContext.Create(logSql: true);

        var result = ctx.Products
            .Where(p => p.Price > 900m)
            .Select(p => new ProductSummaryDto { Id = p.Id, Name = p.Name })
            .ToList();

        Console.WriteLine($"   → {result.Count} DTOs matching Price > 900");
        Console.WriteLine("   ↑ WHERE and the narrow SELECT were both inside one SQL statement.");
        Console.WriteLine("     Nothing evaluated in C# until materialisation.");
        Console.WriteLine();
    }
}
