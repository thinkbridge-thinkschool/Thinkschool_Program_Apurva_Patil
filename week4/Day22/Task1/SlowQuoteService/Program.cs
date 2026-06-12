var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Thread-safe flaky counter: fails on calls 1 & 2, succeeds on call 3, then resets.
// int[] so the element can be passed as ref inside the lambda closure.
var flake = new int[1];

app.MapGet("/external/quote", async (string? mode, CancellationToken ct) =>
{
    switch ((mode ?? "ok").ToLowerInvariant())
    {
        case "slow":
            // Hangs for 15 s — Polly timeout (3 s) will fire well before this completes.
            await Task.Delay(15_000, ct);
            return Results.Ok(new { quote = "Slow and steady." });

        case "fail":
            return Results.Problem("Simulated server error.", statusCode: 500);

        case "flaky":
        {
            var n = Interlocked.Increment(ref flake[0]);
            if (n % 3 != 0)
                return Results.Problem($"Flaky call #{n} failed.", statusCode: 500);
            Interlocked.Exchange(ref flake[0], 0);   // reset for next retry chain
            return Results.Ok(new { quote = $"Flaky call #{n} succeeded!" });
        }

        default:
            await Task.Delay(100, ct);
            return Results.Ok(new { quote = "The only way to do great work is to love what you do. — Steve Jobs" });
    }
});

app.Run();
