using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options) { }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Author).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(80);
            entity.Property(e => e.OwnerId).IsRequired();

            // CollectionItem is an owned type stored in its own table.
            // The composite key (CollectionId, QuoteId) is a natural fit — each quote
            // can only appear once per collection, so this pair is always unique.
            entity.OwnsMany(c => c.Items, item =>
            {
                item.WithOwner().HasForeignKey("CollectionId");
                item.Property(i => i.QuoteId).IsRequired().ValueGeneratedNever();
                item.Property(i => i.AddedAt).IsRequired();
                item.Property<int>("CollectionId").ValueGeneratedNever();
                item.HasKey("CollectionId", nameof(CollectionItem.QuoteId));
            });

            // Tell EF Core to use the private _items backing field
            // because Items is exposed as read-only IReadOnlyList<>
            entity.Navigation(c => c.Items)
                .HasField("_items")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(50);
            // Unique index makes duplicate MessageId inserts fail fast at the DB level —
            // the AnyAsync check above is the fast path; this is the safety net.
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.Property(e => e.ProcessedAt).IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ProcessedAt);
            entity.Property(e => e.Attempts).HasDefaultValue(0);
            // Index speeds up the relay's WHERE ProcessedAt IS NULL poll
            entity.HasIndex(e => e.ProcessedAt);
        });
    }
}
