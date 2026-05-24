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
    private static readonly ActivitySource _activitySource = new("QuotesApi");

    private readonly QuotesDbContext _db;
    private readonly IClock _clock;

    public QuotesController(QuotesDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _db.Quotes.ToListAsync());
    }

    [HttpGet("slow-nplusone")]
    public async Task<IActionResult> GetAllSlowNPlusOne(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("quotes-optimized-load");

        var result = await _db.Quotes
            .OrderBy(q => q.Id)
            .Take(50)
            .Select(q => new QuoteEntity
            {
                Id = q.Id,
                OwnerId = q.OwnerId,
                Text = q.Text,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync(cancellationToken);

        activity?.SetTag("quotes.count", result.Count);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var quote = await _db.Quotes.FindAsync(id);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteRequest req)
    {
        var userId = User.FindFirst("sub")?.Value
                  ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? "unknown";

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

    [HttpPut("{id:int}")]
    [Authorize(Policy = "can-edit-quotes")]
    public IActionResult Edit(int id, [FromBody] string text)
    {
        return Ok(new { Id = id, Text = text, Updated = true });
    }

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
