using System.Text.Json;
using System.Threading.Channels;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

record TokenRequest(string UserId, string[] Scopes);
record TokenResponse(string AccessToken);
record PublishRequest(int OrderId, string Action = "process");

// Request body types for collection endpoints
record CreateCollectionRequest(string Name, int OwnerId);
record AddQuoteRequest(int QuoteId);

public static class EndpointExtensions
{
    /// <summary>
    /// Map all Quote API endpoints
    /// </summary>
    public static WebApplication MapQuoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/quotes")
            .WithName("Quotes");

        // GET /api/quotes - Get all quotes with pagination
        group.MapGet("/", GetAllQuotes)
            .WithName("GetAllQuotes")
            .WithDescription("Get all quotes with pagination")
            .RequireAuthorization();

        // GET /api/quotes/{id} - Get quote by ID
        group.MapGet("/{id}", GetQuoteById)
            .WithName("GetQuoteById")
            .WithDescription("Get a specific quote by ID")
            .RequireAuthorization();

        // POST /api/quotes - Create new quote
        group.MapPost("/", CreateQuote)
            .WithName("CreateQuote")
            .WithDescription("Create a new quote");

        // DELETE /api/quotes/{id} - Delete quote by ID
        group.MapDelete("/{id}", DeleteQuote)
            .WithName("DeleteQuote")
            .WithDescription("Delete a quote by ID");

            

        return app;
    }

    /// <summary>
    /// GET /api/quotes - Get all quotes with pagination
    /// Query params: page (default 1), size (default 10)
    /// </summary>
    private static async Task<IResult> GetAllQuotes(
        int page = 1,
        int size = 10,
        IQuoteRepository repository = default!,
        HybridCache cache = default!,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var cacheKey = $"quotes:page={page}:size={size}";

        var quotes = await cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                Console.WriteLine($"[DB HIT] Fetching quotes from database — page={page}, size={size}");
                return await repository.GetAllAsync(page, size, ct);
            },
            cancellationToken: cancellationToken);

        return Results.Ok(quotes);
    }

    /// <summary>
    /// GET /api/quotes/{id} - Get quote by ID
    /// </summary>
    private static async Task<IResult> GetQuoteById(
        int id,
        IQuoteRepository repository,
        CancellationToken cancellationToken)
    {
        if (id < 1)
            return Results.BadRequest(new { error = "Invalid quote ID" });

        var quote = await repository.GetByIdAsync(id, cancellationToken);

        return quote is null
            ? Results.NotFound(new { error = "Quote not found" })
            : Results.Ok(quote);
    }

    /// <summary>
    /// POST /api/quotes - Create a new quote
    /// Body: { "author": "string", "text": "string" }
    /// </summary>
    private static async Task<IResult> CreateQuote(
        Quote quote,
        QuotesDbContext db,
        Channel<int> notificationChannel,
        CancellationToken cancellationToken)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(quote.Author) ||
            string.IsNullOrWhiteSpace(quote.Text))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["error"] = new[] { "Author and Text are required fields" }
                });
        }

        // Explicit transaction: Quote row + OutboxMessage row commit together or not at all.
        // A crash before CommitAsync rolls back both — the queue never diverges from the DB.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        quote.CreatedAt = DateTime.UtcNow;
        db.Quotes.Add(quote);

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "QuoteCreated",
            Payload = JsonSerializer.Serialize(quote),
            CreatedAt = DateTime.UtcNow
        };
        db.OutboxMessages.Add(outbox);

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        notificationChannel.Writer.TryWrite(quote.Id);
        return Results.Created($"/api/quotes/{quote.Id}", quote);
    }

    /// <summary>
    /// DELETE /api/quotes/{id} - Delete a quote
    /// </summary>
    private static async Task<IResult> DeleteQuote(
        int id,
        IQuoteRepository repository,
        CancellationToken cancellationToken)
    {
        if (id < 1)
            return Results.BadRequest(new { error = "Invalid quote ID" });

        var deleted = await repository.DeleteAsync(id, cancellationToken);

        return deleted
            ? Results.NoContent()
            : Results.NotFound(new { error = "Quote not found" });
    }

    // =========================================================================
    // Auth Endpoints
    // =========================================================================

    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/token", (TokenRequest request, ITokenService tokenService) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["userId"] = ["UserId is required."] });

            var accessToken = tokenService.CreateToken(request.UserId, request.Scopes ?? []);
            return Results.Ok(new TokenResponse(accessToken));
        })
        .WithName("GetToken")
        .WithDescription("Issue a signed JWT")
        .AllowAnonymous();

        return app;
    }

    // =========================================================================
    // Collection Endpoints
    // =========================================================================

    /// <summary>
    /// Map all Collection API endpoints
    /// </summary>
    public static WebApplication MapCollectionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/collections")
            .WithName("Collections");

        // POST /api/collections - Create a new collection
        group.MapPost("/", CreateCollection)
            .WithName("CreateCollection")
            .WithDescription("Create a new collection");

        // POST /api/collections/{id}/items - Add a quote to a collection
        group.MapPost("/{id}/items", AddQuoteToCollection)
            .WithName("AddQuoteToCollection")
            .WithDescription("Add a quote to a collection");

        // DELETE /api/collections/{id}/items/{quoteId} - Remove a quote from a collection
        group.MapDelete("/{id}/items/{quoteId}", RemoveQuoteFromCollection)
            .WithName("RemoveQuoteFromCollection")
            .WithDescription("Remove a quote from a collection");

        return app;
    }

    /// <summary>
    /// POST /api/collections - Create a new collection
    /// Body: { "name": "string", "ownerId": 1 }
    /// </summary>
    private static async Task<IResult> CreateCollection(
        CreateCollectionRequest request,
        ICollectionRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["name"] = new[] { "Name is required." }
                });
        }

        if (request.OwnerId < 1)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["ownerId"] = new[] { "OwnerId must be a positive number." }
                });
        }

        try
        {
            var collection = new Collection(request.Name, request.OwnerId);
            var created = await repository.AddAsync(collection, cancellationToken);
            return Results.Created($"/api/collections/{created.Id}", created);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["name"] = new[] { ex.Message }
                });
        }
    }

    /// <summary>
    /// POST /api/collections/{id}/items - Add a quote to a collection
    /// Body: { "quoteId": 1 }
    /// </summary>
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
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // =========================================================================
    // Message Endpoints (Day 19 — Azure Service Bus)
    // =========================================================================

    /// <summary>
    /// Map all Service Bus demo endpoints.
    /// Returns 503 when Service Bus is not configured so the rest of the app still works.
    /// </summary>
    public static WebApplication MapMessageEndpoints(this WebApplication app)
    {
        // Auth intentionally omitted — demo endpoints only, not for production.
        var group = app.MapGroup("/api/messages");

        // POST /api/messages/publish — Part 1: send a normal order message to the topic
        group.MapPost("/publish", async (PublishRequest req, HttpContext ctx, CancellationToken ct) =>
        {
            var pub = ctx.RequestServices.GetService<ServiceBusPublisher>();
            if (pub is null)
                return Results.Problem("Service Bus is not configured.", statusCode: 503);

            await pub.SendOrderMessageAsync(req.OrderId, req.Action, ct);
            return Results.Ok(new { message = $"Message queued for orderId={req.OrderId}" });
        })
        .WithName("PublishMessage")
        .WithDescription("Publish an order message to the Service Bus topic");

        // POST /api/messages/publish-poison — Part 4: send malformed JSON that will dead-letter
        group.MapPost("/publish-poison", async (HttpContext ctx, CancellationToken ct) =>
        {
            var pub = ctx.RequestServices.GetService<ServiceBusPublisher>();
            if (pub is null)
                return Results.Problem("Service Bus is not configured.", statusCode: 503);

            await pub.SendPoisonMessageAsync(ct);
            return Results.Ok(new { message = "Poison message sent — watch the consumer logs; it will dead-letter after 3 failed attempts" });
        })
        .WithName("PublishPoisonMessage")
        .WithDescription("Send a malformed message to demonstrate DLQ behaviour");

        // GET /api/messages/dlq — Part 4 proof: read and log whatever is in the dead-letter sub-queue
        group.MapGet("/dlq", async (HttpContext ctx) =>
        {
            var sbClient = ctx.RequestServices.GetService<ServiceBusClient>();
            if (sbClient is null)
                return Results.Problem("Service Bus is not configured.", statusCode: 503);

            var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
            var topicName = config["ServiceBus:TopicName"];
            if (string.IsNullOrWhiteSpace(topicName))
                return Results.Problem("ServiceBus:TopicName is not configured.", statusCode: 503);
            var primarySub = config["ServiceBus:PrimarySubscription"];
            if (string.IsNullOrWhiteSpace(primarySub))
                return Results.Problem("ServiceBus:PrimarySubscription is not configured.", statusCode: 503);
            var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(EndpointExtensions));

            await using var receiver = sbClient.CreateReceiver(
                topicName,
                primarySub,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            var messages = await receiver.PeekMessagesAsync(maxMessages: 10);

            var results = new List<object>();
            foreach (var msg in messages)
            {
                logger.LogWarning(
                    "DLQ message: MessageId={MessageId} Reason={Reason} Body={Body}",
                    msg.MessageId, msg.DeadLetterReason, msg.Body);

                results.Add(new
                {
                    msg.MessageId,
                    Body = msg.Body.ToString(),
                    msg.DeadLetterReason,
                    msg.DeadLetterErrorDescription,
                    EnqueuedAt = msg.EnqueuedTime
                });
            }

            return Results.Ok(new { count = results.Count, messages = results });
        })
        .WithName("ReadDeadLetterQueue")
        .WithDescription("Read and drain the dead-letter sub-queue (Part 4 proof)");

        return app;
    }

    /// <summary>
    /// DELETE /api/collections/{id}/items/{quoteId} - Remove a quote from a collection
    /// </summary>
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
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
