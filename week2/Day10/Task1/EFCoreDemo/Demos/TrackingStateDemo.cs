using Microsoft.EntityFrameworkCore;

namespace EFCoreDemo.Demos;

public static class TrackingStateDemo
{
    public static void Run(AppDbContext ctx)
    {
        Console.WriteLine("=== Tracking State ===");
        Console.WriteLine();

        // ── Baseline: tracker is empty ────────────────────────────────────────
        Console.WriteLine($"Tracker entries before any query : {ctx.ChangeTracker.Entries().Count()}");

        // ── Tracked query registers 5 EntityEntry objects ────────────────────
        // Each entry holds: the entity, its state (Unchanged/Modified/…),
        // and an original-values snapshot for dirty detection.
        var products = ctx.Products.Take(5).ToList();
        Console.WriteLine($"Tracker entries after Take(5)    : {ctx.ChangeTracker.Entries().Count()}");

        Console.WriteLine("\nInitial states:");
        foreach (var entry in ctx.ChangeTracker.Entries())
            Console.WriteLine($"  [{entry.State,-9}] Id={((dynamic)entry.Entity).Id,-5} Name={(string)((dynamic)entry.Entity).Name}");

        // ── Mutation detection (snapshot comparison) ─────────────────────────
        // EF Core compares the current property values against the original-values
        // snapshot on SaveChanges (or DetectChanges).  No attribute or proxy
        // needed — plain POCO with auto-properties is enough.
        products[0].Price = 9_999m;
        products[2].Stock = 0;

        // Force the tracker to scan for changes right now (SaveChanges also
        // calls this internally).
        ctx.ChangeTracker.DetectChanges();

        Console.WriteLine("\nAfter mutating products[0].Price and products[2].Stock:");
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            var e = (dynamic)entry.Entity;
            Console.WriteLine($"  [{entry.State,-9}] Id={e.Id,-5} Price={e.Price,8} Stock={e.Stock}");
        }

        // SaveChanges would emit exactly two UPDATE statements — one per
        // Modified row — and leave the three Unchanged rows untouched.
        Console.WriteLine("\n(SaveChanges skipped — demo only; would issue 2 UPDATE statements)");
        Console.WriteLine();

        // ── AsNoTracking: zero entries added ─────────────────────────────────
        ctx.ChangeTracker.Clear();
        _ = ctx.Products.AsNoTracking().Take(5).ToList();
        Console.WriteLine($"Tracker entries after AsNoTracking Take(5): {ctx.ChangeTracker.Entries().Count()}");
        Console.WriteLine("No snapshots allocated, no change detection overhead.\n");
    }
}
