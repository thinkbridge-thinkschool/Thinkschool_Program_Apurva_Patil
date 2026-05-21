using Microsoft.AspNetCore.Mvc;
using Task04.Riches.Domain;

namespace Task04.Riches.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    // In a real app this would be a repository; here we keep a simple in-memory list
    private static readonly List<Quote> _store = new();

    [HttpPost]
    public IActionResult Create([FromBody] CreateQuoteDto dto)
    {
        var result = Quote.Create(dto.Author ?? string.Empty, dto.Text ?? string.Empty);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        var q = result.Value!;
        q.GetType().GetProperty("Id")!.SetValue(q, _store.Count + 1);
        _store.Add(q);
        return CreatedAtAction(nameof(Get), new { id = q.Id }, new { q.Id, q.Author, q.Text });
    }

    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        var q = _store.FirstOrDefault(x => x.Id == id);
        if (q == null) return NotFound();
        return Ok(new { q.Id, q.Author, q.Text, q.IsDeleted });
    }

    [HttpPost("{id}/soft-delete")]
    public IActionResult SoftDelete(int id)
    {
        var q = _store.FirstOrDefault(x => x.Id == id);
        if (q == null) return NotFound();
        q.SoftDelete();
        return NoContent();
    }
}

public record CreateQuoteDto(string? Author, string? Text);
