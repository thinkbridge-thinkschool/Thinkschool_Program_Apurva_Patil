using BenchmarkDotNet.Attributes;
using EFCoreDemo;
using Microsoft.EntityFrameworkCore;

// Config is injected via Program.cs ManualConfig — no inline attributes needed.
public class ReadBenchmark
{
    // BenchmarkDotNet creates a fresh class instance per iteration, so we
    // seed once in GlobalSetup rather than in the constructor.
    [GlobalSetup]
    public void Setup()
    {
        using var ctx = AppDbContext.Create();
        AppDbContext.SeedIfEmpty(ctx);
    }

    // ── Variant 1: default tracked read ──────────────────────────────────────
    // For each of the 10 000 rows EF Core:
    //   1. Materialises a Product instance
    //   2. Allocates an EntityEntry<Product> wrapper
    //   3. Takes an original-values snapshot (a property-value copy)
    //   4. Registers the entry in the identity map (dictionary lookup/insert)
    // All of that bookkeeping shows up as extra allocations and time.
    [Benchmark(Baseline = true)]
    public int WithTracking()
    {
        using var ctx = AppDbContext.Create();
        return ctx.Products.ToList().Count;
    }

    // ── Variant 2: AsNoTracking ───────────────────────────────────────────────
    // EF Core materialises each row into a Product but skips steps 2-4 entirely.
    // No EntityEntry, no snapshot copy, no identity-map registration.
    // Pure read — faster and allocates roughly half the memory.
    [Benchmark]
    public int WithoutTracking()
    {
        using var ctx = AppDbContext.Create();
        return ctx.Products.AsNoTracking().ToList().Count;
    }
}
