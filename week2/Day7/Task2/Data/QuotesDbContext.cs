using Microsoft.EntityFrameworkCore;

namespace QuotesDay7Task2.Data;

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options) { }

    public DbSet<Author>         Authors         => Set<Author>();
    public DbSet<Quote>          Quotes          => Set<Quote>();
    public DbSet<QuoteWindowRow> QuoteWindowRows => Set<QuoteWindowRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Bio).HasMaxLength(1000);
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Author)
                  .WithMany(a => a.Quotes)
                  .HasForeignKey(e => e.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // QuoteWindowRow is a CTE projection — no backing table
        modelBuilder.Entity<QuoteWindowRow>().HasNoKey().ToView(null);
    }
}
