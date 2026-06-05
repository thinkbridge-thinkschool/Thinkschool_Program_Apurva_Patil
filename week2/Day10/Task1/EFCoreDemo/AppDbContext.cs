using EFCoreDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemo;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    // LocalDB — consistent with the rest of the week's projects.
    private const string ConnectionString =
        @"Server=(localdb)\mssqllocaldb;Database=EFCoreDemoDay10;Trusted_Connection=True;TrustServerCertificate=True";

    public static AppDbContext Create()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(opts);
    }

    // Creates the schema if it doesn't exist, then seeds 10 000 rows.
    // The cross-join tally avoids the 100-row default MAXRECURSION limit of
    // recursive CTEs and mirrors what DBSetup.sql does via SQL.
    public static void SeedIfEmpty(AppDbContext ctx)
    {
        ctx.Database.EnsureCreated();
        if (ctx.Products.Any()) return;

        var products = Enumerable.Range(1, 10_000).Select(i => new Product
        {
            Name  = $"Product-{i}",
            Price = Math.Round(1m + (i % 999), 2),
            Stock = i % 500
        });

        ctx.Products.AddRange(products);
        ctx.SaveChanges();
        Console.WriteLine("Seeded 10 000 products.");
    }
}
