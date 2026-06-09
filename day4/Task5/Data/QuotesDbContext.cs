using Microsoft.EntityFrameworkCore;

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options) { }

    public DbSet<QuoteEntity> Quotes => Set<QuoteEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuoteEntity>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.OwnerId).IsRequired().HasMaxLength(100);
            entity.Property(q => q.Text).IsRequired().HasMaxLength(500);
        });
    }
}
