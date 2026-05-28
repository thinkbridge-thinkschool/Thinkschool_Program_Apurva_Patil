using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QueryTranslationDemo.Models;

namespace QueryTranslationDemo;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    // Reuse the same database seeded by Day10/Task1 so we don't re-seed 10 000 rows.
    private const string ConnectionString =
        @"Server=(localdb)\mssqllocaldb;Database=EFCoreDemoDay10;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>
    /// logSql=true restricts logging to DB command events only (no EF internal noise)
    /// and enables parameter values so the literal WHERE values appear in the output.
    /// </summary>
    public static AppDbContext Create(bool logSql = false)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString);

        if (logSql)
            builder
                .LogTo(
                    Console.WriteLine,
                    new[] { DbLoggerCategory.Database.Command.Name },
                    LogLevel.Information)
                .EnableSensitiveDataLogging();

        return new AppDbContext(builder.Options);
    }
}
