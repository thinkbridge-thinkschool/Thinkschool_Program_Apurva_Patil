using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

[Authorize]
[ApiController]
[Route("[controller]")]
public class QuotesController : ControllerBase
{
    // One ActivitySource per service — reused across all requests.
    private static readonly ActivitySource _activitySource = new("QuotesApi");

    private readonly QuotesDbContext _db;
    private readonly IClock _clock;

    public QuotesController(QuotesDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // GET /quotes — any authenticated user; returns all quotes from DB
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _db.Quotes.ToListAsync());
    }

    // GET /quotes/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var quote = await _db.Quotes.FindAsync(id);
        return quote is null ? NotFound() : Ok(quote);
    }

    // POST /quotes — creates a quote; text is validated via model annotations
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteRequest req)
    {
        var userId = User.FindFirst("sub")?.Value
                  ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? "unknown";

        // Custom span: covers the build-and-persist step that isn't
        // automatically instrumented by OTel AspNetCore or EF Core.
        using var activity = _activitySource.StartActivity("persist-quote");
        activity?.SetTag("user.id", userId);
        activity?.SetTag("quote.text.length", req.Text.Length);

        var entity = new QuoteEntity
        {
            OwnerId   = userId,
            Text      = req.Text,
            CreatedAt = _clock.UtcNow
        };
        _db.Quotes.Add(entity);
        await _db.SaveChangesAsync();

        activity?.SetTag("quote.id", entity.Id);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    // PUT /quotes/{id} — requires scope claim "quotes.write"
    [HttpPut("{id:int}")]
    [Authorize(Policy = "can-edit-quotes")]
    public IActionResult Edit(int id, [FromBody] string text)
    {
        return Ok(new { Id = id, Text = text, Updated = true });
    }

    // DELETE /quotes/{id}/owner/{ownerId}
    // The route exposes ownerId so QuoteOwnerHandler can compare it against sub claim.
    [HttpDelete("{id:int}/owner/{ownerId}")]
    [Authorize(Policy = "can-delete-own-quote")]
    public IActionResult Delete(int id, string ownerId)
    {
        return Ok(new { Id = id, Deleted = true });
    }
}

public record CreateQuoteRequest(
    [Required(AllowEmptyStrings = false)]
    [MaxLength(500)]
    string Text
);
