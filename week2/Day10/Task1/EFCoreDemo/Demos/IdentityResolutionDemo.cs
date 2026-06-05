using EFCoreDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemo.Demos;

public static class IdentityResolutionDemo
{
    public static void Run(AppDbContext ctx)
    {
        Console.WriteLine("=== Identity Resolution ===");
        Console.WriteLine();

        // ── Tracked queries ──────────────────────────────────────────────────
        // The change tracker maintains an identity map keyed by primary key.
        // Two separate First() calls for the same PK inside the SAME DbContext
        // scope return the EXACT SAME object from the map — no second DB round
        // trip, no second allocation.
        var a = ctx.Products.First(p => p.Id == 1);
        var b = ctx.Products.First(p => p.Id == 1);

        Console.WriteLine($"[Tracked] Same object reference for PK=1? {ReferenceEquals(a, b)}");
        Console.WriteLine($"  a.Name = \"{a.Name}\"  |  b.Name = \"{b.Name}\"");

        // Because a and b are the same object, mutating via one handle is
        // immediately visible through the other — no stale copy problem.
        a.Name = "MUTATED-VIA-A";
        Console.WriteLine($"  After a.Name = \"MUTATED-VIA-A\"  →  b.Name = \"{b.Name}\"");
        Console.WriteLine($"  Tracker state of entry: {ctx.Entry(a).State}");
        Console.WriteLine();

        // ── AsNoTracking queries ─────────────────────────────────────────────
        // Without the identity map, every materialisation produces a NEW object.
        // Two calls for the same PK yield two independent instances.
        var c = ctx.Products.AsNoTracking().First(p => p.Id == 2);
        var d = ctx.Products.AsNoTracking().First(p => p.Id == 2);

        Console.WriteLine($"[Untracked] Same object reference for PK=2? {ReferenceEquals(c, d)}");
        Console.WriteLine($"  c.Name = \"{c.Name}\"  |  d.Name = \"{d.Name}\"");

        c.Name = "MUTATED-VIA-C";
        Console.WriteLine($"  After c.Name = \"MUTATED-VIA-C\"  →  d.Name = \"{d.Name}\" (unchanged)");
        Console.WriteLine($"  Tracker entries after untracked queries: {ctx.ChangeTracker.Entries().Count()}");
        Console.WriteLine();

        // ── Why this matters ─────────────────────────────────────────────────
        // If you load the same entity twice in a unit of work with AsNoTracking
        // and mutate one copy, the mutation is NOT visible through the other.
        // That silent divergence is a real bug in update/aggregate workflows.
        Console.WriteLine("Key insight: identity map == correctness guarantee, not just a cache.");
        Console.WriteLine();
    }
}
