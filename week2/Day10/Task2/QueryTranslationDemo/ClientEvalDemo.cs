using Microsoft.EntityFrameworkCore;

namespace QueryTranslationDemo;

public static class ClientEvalDemo
{
    // ── Demo: Accidental client-side evaluation — caught and fixed ────────────
    // EF Core 3+ throws InvalidOperationException for expressions it cannot
    // translate to SQL.  But one trap does NOT throw — it silently fetches
    // far more data than needed:
    //
    //   Inserting .AsEnumerable() mid-query shifts the evaluation boundary
    //   from the database to the client.  Everything AFTER that call becomes
    //   LINQ to Objects — executed in C# after EF has already fetched rows.
    //
    // THE TRAP:
    //   ctx.Products                        ← IQueryable<Product> — no SQL yet
    //     .AsNoTracking()
    //     .AsEnumerable()                   ← switches to IEnumerable<Product>
    //     .Where(p => p.Price < 5m)         ← runs in C#, NOT translated to SQL WHERE
    //     .Take(10)                         ← runs in C#, NOT translated to SQL TOP
    //     .ToList()                         ← SQL fires here — all 10 000 rows fetched
    //
    //   SQL sent: SELECT all columns FROM [Products]  (all 10 000 rows!)
    //   C# then: scans 10 000 objects, keeps ~50, returns first 10
    //
    // HOW TO CATCH IT:
    //   Read the logged SQL.  A bare SELECT with no WHERE and no TOP when you
    //   expected a filtered query is the signature of accidental client eval.
    //
    // THE FIX:
    //   Remove .AsEnumerable(). Keep the full pipeline as IQueryable<T> until
    //   .ToList().  Where() and Take() are then translated to SQL.
    public static void Run()
    {
        Console.WriteLine("── Client-side evaluation — .AsEnumerable() silent trap ─────────────────");

        // ── BEFORE (broken) — .AsEnumerable() forces a full table scan ───────
        Console.WriteLine("   [BEFORE — .AsEnumerable() inserted mid-query]");
        Console.WriteLine("   Intent  : fetch 10 products with Price < 5");
        Console.WriteLine("   Reality : SQL fetches ALL 10 000 rows; C# does the filtering");
        Console.WriteLine();

        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var broken = ctx.Products
                .AsNoTracking()
                .AsEnumerable()              // ← evaluation boundary shifts here
                .Where(p => p.Price < 5m)   // ← C# filter, not SQL WHERE
                .Take(10)                    // ← C# take, not SQL TOP
                .ToList();                   // ← all 10 000 rows already fetched

            Console.WriteLine($"   → {broken.Count} rows returned — but 10 000 were transferred.");
            Console.WriteLine("   ↑ No WHERE, no TOP in the SQL above. That is the bug.");
        }
        Console.WriteLine();

        // ── AFTER (fixed) — filter and take stay in IQueryable ───────────────
        Console.WriteLine("   [AFTER — .Where() and .Take() before materialisation, no .AsEnumerable()]");
        Console.WriteLine("   Expected SQL: WHERE [Price] < 5.0  +  TOP(10)");
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
