using Microsoft.EntityFrameworkCore;

namespace QueryTranslationDemo.Demos;

public static class ClientSideEvalDemo
{
    public static void Run()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  CLIENT-SIDE EVALUATION DEMO — .AsEnumerable() silent trap              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── BAD: .AsEnumerable() inserted mid-query ───────────────────────────────
        // This is the most dangerous form of accidental client-side evaluation:
        // NO exception is thrown and NO warning is printed.
        //
        // What happens step by step:
        //   .AsEnumerable()    shifts the evaluation boundary from IQueryable to IEnumerable.
        //   Everything AFTER it becomes LINQ-to-Objects, running on the C# heap.
        //   The .Where() and .Take() that follow are invisible to EF — they are
        //   never translated to SQL.
        //
        // SQL sent: SELECT [Id],[Name],[Price],[Stock] FROM [Products]  — no WHERE, no TOP
        // Then C# scans all 10 000 objects, keeps ~50, returns first 10.
        // 10 000 rows crossed the network.  10 were needed.
        //
        // HOW TO DETECT IT: read the logged SQL.
        // A bare SELECT with no WHERE and no TOP when your code has .Where() and .Take()
        // is the signature of accidental client-side evaluation.
        Console.WriteLine("── BAD: .AsEnumerable() inserted mid-query (silent — no exception) ────────");
        Console.WriteLine("   Intent  : fetch 10 products with Price < 5");
        Console.WriteLine("   Reality : SQL has no WHERE and no TOP → all 10 000 rows cross the wire");
        Console.WriteLine("   Clue    : look at the SQL below — count the clauses.");
        Console.WriteLine();
        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var broken = ctx.Products
                .AsNoTracking()
                .AsEnumerable()              // ← evaluation boundary shifts HERE
                .Where(p => p.Price < 5m)   // ← C# filter, NOT translated to SQL WHERE
                .Take(10)                    // ← C# take,   NOT translated to SQL TOP
                .ToList();                   // ← all 10 000 rows already fetched by this point

            Console.WriteLine($"   → {broken.Count} rows returned — but 10 000 were transferred.");
            Console.WriteLine("   ↑ No WHERE, no TOP in the SQL above. That is the bug.");
        }
        Console.WriteLine();

        // ── GOOD: keep the full pipeline as IQueryable ────────────────────────────
        // Remove .AsEnumerable(). Where() and Take() stay in IQueryable scope so
        // EF Core translates them to WHERE [p].[Price] < 5.0 and TOP(10) in SQL.
        // Only the 10 matching rows ever leave the database.
        Console.WriteLine("── GOOD: .Where() and .Take() stay IQueryable (no .AsEnumerable()) ────────");
        Console.WriteLine("   Expected SQL: WHERE [p].[Price] < 5.0 + TOP(10)");
        Console.WriteLine();
        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var fixed_ = ctx.Products
                .AsNoTracking()
                .Where(p => p.Price < 5m)   // ← translated to SQL WHERE [p].[Price] < 5.0
                .Take(10)                    // ← translated to SQL TOP(10)
                .ToList();                   // ← only the matching rows are fetched

            Console.WriteLine($"   → {fixed_.Count} rows returned and transferred.");
            Console.WriteLine("   ↑ WHERE and TOP are both present in the SQL above.");
        }
        Console.WriteLine();

        Console.WriteLine("   Root cause  : .AsEnumerable() silently shifts evaluation from");
        Console.WriteLine("                 SQL Server to the C# heap. No exception is raised.");
        Console.WriteLine("   Detection   : log the SQL. Bare SELECT with no WHERE/TOP when your");
        Console.WriteLine("                 code has .Where()/.Take() = accidental client eval.");
        Console.WriteLine("   Fix         : keep the full pipeline as IQueryable<T>; only call");
        Console.WriteLine("                 .ToList() at the very end.");
        Console.WriteLine();
    }
}
