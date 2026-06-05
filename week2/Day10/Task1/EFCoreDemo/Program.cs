using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using EFCoreDemo;
using EFCoreDemo.Demos;

// ── Seed ─────────────────────────────────────────────────────────────────────
using (var ctx = AppDbContext.Create())
    AppDbContext.SeedIfEmpty(ctx);

// ── Demos ─────────────────────────────────────────────────────────────────────
using (var ctx = AppDbContext.Create())
    IdentityResolutionDemo.Run(ctx);

using (var ctx = AppDbContext.Create())
    TrackingStateDemo.Run(ctx);

// ── Benchmark ─────────────────────────────────────────────────────────────────
// BenchmarkDotNet REQUIRES a Release build — it will refuse to run under Debug.
// Run with:  dotnet run -c Release -- --benchmark
//
// InProcessEmitToolchain avoids the out-of-process project-file discovery that
// fails on OneDrive paths (Windows junction 'Application Data' is denied).
// Both variants still run under identical in-process conditions so the
// measured ratio is accurate.
if (args.Contains("--benchmark"))
{
    var config = ManualConfig.Create(DefaultConfig.Instance)
        .AddJob(Job.Default
            .WithWarmupCount(2)
            .WithIterationCount(5)
            .WithToolchain(InProcessEmitToolchain.Instance))
        .AddDiagnoser(MemoryDiagnoser.Default)
        .AddExporter(JsonExporter.Default)
        .WithOption(ConfigOptions.DisableOptimizationsValidator, true);

    BenchmarkRunner.Run<ReadBenchmark>(config);
    return;
}

Console.WriteLine("""
─────────────────────────────────────────────────────────────────────────────
 Demos complete.
 To measure allocations and time on 10 000 rows run:
   dotnet run -c Release -- --benchmark
─────────────────────────────────────────────────────────────────────────────
""");
