using Microsoft.EntityFrameworkCore;

namespace QuotesDay7Task3.Data;

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options) { }

    public DbSet<Author>         Authors          => Set<Author>();
    public DbSet<Quote>          Quotes           => Set<Quote>();
    public DbSet<Tag>            Tags             => Set<Tag>();
    public DbSet<QuoteTag>       QuoteTags        => Set<QuoteTag>();
    public DbSet<AuthorCategory> AuthorCategories => Set<AuthorCategory>();
    public DbSet<AuthorNameRow>  AuthorNameRows   => Set<AuthorNameRow>();
    public DbSet<TagNameRow>     TagNameRows      => Set<TagNameRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Author>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Bio).HasMaxLength(1000);
        });

        modelBuilder.Entity<Quote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).IsRequired().HasMaxLength(1000);
            e.Property(x => x.IsDeleted).HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(x => x.Author)
             .WithMany(a => a.Quotes)
             .HasForeignKey(x => x.AuthorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<QuoteTag>(e =>
        {
            e.HasKey(x => new { x.QuoteId, x.TagId });
            e.HasOne(x => x.Quote)
             .WithMany(q => q.QuoteTags)
             .HasForeignKey(x => x.QuoteId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag)
             .WithMany(t => t.QuoteTags)
             .HasForeignKey(x => x.TagId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuthorCategory>(e =>
        {
            e.HasKey(x => new { x.AuthorId, x.Category });
            e.Property(x => x.Category).HasMaxLength(50);
            e.HasOne(x => x.Author)
             .WithMany(a => a.Categories)
             .HasForeignKey(x => x.AuthorId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Keyless projections — CTE result types, no backing table
        modelBuilder.Entity<AuthorNameRow>().HasNoKey().ToView(null);
        modelBuilder.Entity<TagNameRow>().HasNoKey().ToView(null);
    }
}
