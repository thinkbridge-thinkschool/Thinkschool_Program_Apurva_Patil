using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace QueryTranslationDemo;

public static class SqlLoggingDemo
{
    // Shows how to wire LogTo so every SQL statement EF sends is printed to
    // the console — filtered to command events only so the output stays readable.
    public static void Run()
    {
        Console.WriteLine("── SQL Logging setup ─────────────────────────────────────────────────────");
        Console.WriteLine("   LogTo(Console.WriteLine, [Database.Command], LogLevel.Information)");
        Console.WriteLine("   + EnableSensitiveDataLogging  → parameter values visible in dev");
        Console.WriteLine();

        // Build options explicitly here so the LogTo configuration is visible.
        // In a real app this lives in AppDbContext.Create(logSql: true).
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(
                @"Server=(localdb)\mssqllocaldb;Database=EFCoreDemoDay10;Trusted_Connection=True;TrustServerCertificate=True")
            .LogTo(
                Console.WriteLine,
                new[] { DbLoggerCategory.Database.Command.Name },
                LogLevel.Information)
            .EnableSensitiveDataLogging()
            .Options;

        // One full-entity query so the logged SQL is visible right away.
        // Notice EF selects ALL four columns even though we only print the Name.
        // That is the inefficiency ProjectionDemo fixes.
        using var ctx = new AppDbContext(opts);
        var products = ctx.Products.Take(3).ToList();

        Console.WriteLine($"   C# received {products.Count} Product objects.");
        Console.WriteLine("   ↑ SQL above fetches Id, Name, Price, Stock — all 4 columns.");
        Console.WriteLine("     Price and Stock cross the wire even though we never use them here.");
        Console.WriteLine();
    }
}
