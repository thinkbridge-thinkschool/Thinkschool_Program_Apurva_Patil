using Microsoft.EntityFrameworkCore;
using QuotesApi.Commands;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

record CreateCollectionRequest(string Name, int OwnerId);
record AddQuoteRequest(int QuoteId);

public static class EndpointExtensions
{
    public static WebApplication MapQuoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/quotes")
            .WithName("Quotes");

        // READ PATH — served by IQuoteQueryService (projection, AsNoTracking, 4 columns)
        group.MapGet("/", GetAllQuotes)
            .WithName("GetAllQuotes")
            .WithDescription("Get all quotes (read model)");

        group.MapGet("/{id}", GetQuoteById)
            .WithName("GetQuoteById")
            .WithDescription("Get a quote by ID (read model)");

        // WRITE PATH — POST goes through CreateQuoteCommandHandler
        group.MapPost("/", CreateQuote)
            .RequireAuthorization()
            .WithName("CreateQuote")
            .WithDescription("Create a new quote (command)");

        // DELETE still loads the domain entity — needs SoftDelete() business method
        group.MapDelete("/{id}", DeleteQuote)
            .RequireAuthorization()
            .WithName("DeleteQuote")
            .WithDescription("Soft-delete a quote");

        return app;
    }

    // ── READ: returns QuoteReadModel — no domain entity, no change tracker ──────
    private static async Task<IResult> GetAllQuotes(
        int page = 1,
        int size = 10,
        IQuoteQueryService queryService = default!,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var quotes = await queryService.GetPagedAsync(page, size, cancellationToken);
        return Results.Ok(quotes);
    }

    private static async Task<IResult> GetQuoteById(
        int id,
        IQuoteQueryService queryService,
        CancellationToken cancellationToken)
    {
        if (id < 1)
            return Results.BadRequest(new { error = "Invalid quote ID" });

        var quote = await queryService.GetByIdAsync(id, cancellationToken);

        return quote is null
            ? Results.NotFound(new { error = "Quote not found" })
            : Results.Ok(quote);
    }

    // ── WRITE: command carries Author + Text, handler owns validation + persistence ─
    private static async Task<IResult> CreateQuote(
        CreateQuoteCommand command,
        CreateQuoteCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var (quoteId, errors) = await handler.HandleAsync(command, cancellationToken);

        if (errors is not null)
            return Results.ValidationProblem(errors);

        return Results.Created($"/api/quotes/{quoteId}", new { id = quoteId });
    }

    // ── DELETE: still uses domain entity — SoftDelete() is a business rule ────────
    private static async Task<IResult> DeleteQuote(
        int id,
        IQuoteRepository repository,
        CancellationToken cancellationToken)
    {
        if (id < 1)
            return Results.BadRequest(new { error = "Invalid quote ID" });

        var quote = await repository.GetByIdAsync(id, cancellationToken);

        if (quote is null)
            return Results.NotFound(new { error = "Quote not found" });

        quote.SoftDelete();
        await repository.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // =========================================================================
    // Collection Endpoints  (unchanged — not part of this CQRS-lite split)
    // =========================================================================

    public static WebApplication MapCollectionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/collections")
            .WithName("Collections");

        group.MapPost("/", CreateCollection)
            .WithName("CreateCollection")
            .WithDescription("Create a new collection");

        group.MapPost("/{id}/items", AddQuoteToCollection)
            .WithName("AddQuoteToCollection")
            .WithDescription("Add a quote to a collection");

        group.MapDelete("/{id}/items/{quoteId}", RemoveQuoteFromCollection)
            .WithName("RemoveQuoteFromCollection")
            .WithDescription("Remove a quote from a collection");

        // ── Dev seed ─────────────────────────────────────────────────────────
        app.MapPost("/api/dev/seed", async (QuotesDbContext db, CancellationToken ct) =>
        {
            db.Quotes.RemoveRange(db.Quotes);
            db.Collections.RemoveRange(db.Collections);
            await db.SaveChangesAsync(ct);

            var authors = new[] { "Marcus Aurelius", "Seneca", "Epictetus", "Lao Tzu", "Confucius" };
            var quotes = new List<Quote>();
            foreach (var author in authors)
                for (var i = 1; i <= 20; i++)
                {
                    var (ok, q, _) = Quote.Create(author, $"Quote {i} by {author}.");
                    if (ok) quotes.Add(q!);
                }

            db.Quotes.AddRange(quotes);
            await db.SaveChangesAsync(ct);

            var now = DateTimeOffset.UtcNow;
            var savedIds = db.Quotes.Select(q => q.Id).Take(30).ToList();
            for (var c = 0; c < 3; c++)
            {
                var col = new Collection($"Collection-{c + 1}", ownerId: 1);
                foreach (var qId in savedIds.Skip(c * 10).Take(10))
                    col.AddItem(qId, now);
                db.Collections.Add(col);
            }
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { quotes = quotes.Count, collections = 3 });
        })
        .WithName("Seed")
        .ExcludeFromDescription();

        // ── SLOW: N+1 ────────────────────────────────────────────────────────
        group.MapGet("/{id}/with-quotes-slow", async (int id, QuotesDbContext db, CancellationToken ct) =>
        {
            var collection = await db.Collections
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (collection is null) return Results.NotFound();

            var details = new List<object>();
            foreach (var item in collection.Items)
            {
                var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == item.QuoteId, ct);
                details.Add(new { item.QuoteId, item.AddedAt, quote?.Author, quote?.Text });
            }

            return Results.Ok(new { collection.Id, collection.Name, Quotes = details });
        })
        .WithName("GetCollectionWithQuotesSlow")
        .ExcludeFromDescription();

        // ── FAST: single IN-clause ────────────────────────────────────────────
        group.MapGet("/{id}/with-quotes-fast", async (int id, QuotesDbContext db, CancellationToken ct) =>
        {
            var collection = await db.Collections
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (collection is null) return Results.NotFound();

            var ids = collection.Items.Select(i => i.QuoteId).ToList();
            var quotes = await db.Quotes
                .Where(q => ids.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, ct);

            var details = collection.Items.Select(item => new
            {
                item.QuoteId,
                item.AddedAt,
                quotes[item.QuoteId].Author,
                quotes[item.QuoteId].Text
            });

            return Results.Ok(new { collection.Id, collection.Name, Quotes = details });
        })
        .WithName("GetCollectionWithQuotesFast")
        .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> CreateCollection(
        CreateCollectionRequest request,
        ICollectionRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "Name is required." }
            });

        if (request.OwnerId < 1)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ownerId"] = new[] { "OwnerId must be a positive number." }
            });

        try
        {
            var collection = new Collection(request.Name, request.OwnerId);
            var created = await repository.AddAsync(collection, cancellationToken);
            return Results.Created($"/api/collections/{created.Id}", created);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = new[] { ex.Message }
            });
        }
    }

    private static async Task<IResult> AddQuoteToCollection(
        int id,
        AddQuoteRequest request,
        ICollectionRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (id < 1)
            return Results.BadRequest(new { error = "Invalid collection ID." });

        if (request.QuoteId < 1)
            return Results.BadRequest(new { error = "Invalid quote ID." });

        var collection = await repository.GetByIdAsync(id, cancellationToken);
        if (collection is null)
            return Results.NotFound(new { error = "Collection not found." });

        try
        {
            collection.AddItem(request.QuoteId, clock.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);
            return Results.Ok(collection);
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499);
        }
    }

    private static async Task<IResult> RemoveQuoteFromCollection(
        int id,
        int quoteId,
        ICollectionRepository repository,
        CancellationToken cancellationToken)
    {
        if (id < 1)
            return Results.BadRequest(new { error = "Invalid collection ID." });

        if (quoteId < 1)
            return Results.BadRequest(new { error = "Invalid quote ID." });

        var collection = await repository.GetByIdAsync(id, cancellationToken);
        if (collection is null)
            return Results.NotFound(new { error = "Collection not found." });

        try
        {
            collection.RemoveItem(quoteId);
            await repository.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(499);
        }
    }
}
