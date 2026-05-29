using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options) { }

    public DbSet<Quote>        Quotes        => Set<Quote>();
    public DbSet<Collection>   Collections   => Set<Collection>();
    public DbSet<User>         Users         => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

       modelBuilder.Entity<Quote>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Text).IsRequired();
    entity.Property(e => e.Author).IsRequired();
    entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    entity.HasIndex(e => e.Author);    // ← add this line
});


        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(80);
            entity.Property(e => e.OwnerId).IsRequired();

            entity.OwnsMany(c => c.Items, item =>
            {
                item.WithOwner().HasForeignKey("CollectionId");
                item.Property(i => i.QuoteId).IsRequired().ValueGeneratedNever();
                item.Property(i => i.AddedAt).IsRequired();
                item.Property<int>("CollectionId").ValueGeneratedNever();
                item.HasKey("CollectionId", nameof(CollectionItem.QuoteId));
            });

            entity.Navigation(c => c.Items)
                .HasField("_items")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // ── User ────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email)
                  .IsRequired()
                  .HasMaxLength(256);
            entity.HasIndex(e => e.Email)
                  .IsUnique();          // no two users with same email
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        // ── RefreshToken ─────────────────────────────────────────────────────
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId);
        });
    }
}