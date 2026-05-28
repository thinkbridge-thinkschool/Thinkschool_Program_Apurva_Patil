using Microsoft.EntityFrameworkCore;
using QueryTranslationDemo.Dtos;

namespace QueryTranslationDemo.Demos;

public static class SqlLoggingDemo
{
    public static void Run()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  SQL LOGGING DEMO — LogTo + EnableSensitiveDataLogging                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ── 1. Full entity — all columns fetched ─────────────────────────────────
        // SQL: SELECT [p].[Id], [p].[Name], [p].[Price], [p].[Stock]
        //      FROM [Products] AS [p]
        //      ORDER BY (SELECT 1) OFFSET 0 ROWS FETCH NEXT 3 ROWS ONLY
        Console.WriteLine("── 1. Full entity query (fetches ALL 4 columns) ──────────────────────────");
        Console.WriteLine("   ctx.Products.Take(3).ToList()");
        Console.WriteLine();
        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var all = ctx.Products.Take(3).ToList();
            Console.WriteLine($"   → {all.Count} rows, columns: Id, Name, Price, Stock");
        }
        Console.WriteLine();

        // ── 2. Projection — only 2 columns cross the wire ────────────────────────
        // SQL: SELECT TOP(3) [p].[Id], [p].[Name]
        //      FROM [Products] AS [p]
        // Price and Stock are NOT in the SELECT list.  The DTO is populated
        // server-side; no full Product entity is ever materialised.
        Console.WriteLine("── 2. Projection query (.Select → Dto, fetches ONLY Id + Name) ──────────");
        Console.WriteLine("   ctx.Products.Select(p => new ProductSummaryDto{...}).Take(3).ToList()");
        Console.WriteLine();
        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var dtos = ctx.Products
                .Select(p => new ProductSummaryDto { Id = p.Id, Name = p.Name })
                .Take(3)
                .ToList();
            Console.WriteLine($"   → {dtos.Count} DTOs, columns: Id, Name  (Price + Stock absent from SQL)");
        }
        Console.WriteLine();

        // ── 3. Filtered projection — WHERE + limited SELECT in one round trip ────
        // SQL: SELECT [p].[Id], [p].[Name]
        //      FROM [Products] AS [p]
        //      WHERE [p].[Price] > 900.0
        Console.WriteLine("── 3. Filtered projection (WHERE + SELECT pushed to SQL) ─────────────────");
        Console.WriteLine("   ctx.Products.Where(p => p.Price > 900m).Select(...).ToList()");
        Console.WriteLine();
        using (var ctx = AppDbContext.Create(logSql: true))
        {
            var expensive = ctx.Products
                .Where(p => p.Price > 900m)
                .Select(p => new ProductSummaryDto { Id = p.Id, Name = p.Name })
                .ToList();
            Console.WriteLine($"   → {expensive.Count} DTOs matching Price > 900 (both WHERE and SELECT in SQL)");
        }
        Console.WriteLine();
    }
}
