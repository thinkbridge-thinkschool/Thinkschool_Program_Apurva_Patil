using Microsoft.AspNetCore.Mvc;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly IQuoteRepository _repo;
    private readonly IClock _clock;
    private readonly IQuoteFormatter _formatter;

    public QuotesController(IQuoteRepository repo, IClock clock, IQuoteFormatter formatter)
    {
        _repo = repo;
        _clock = clock;
        _formatter = formatter;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var quotes = _repo.GetAll();
        var now = _clock.UtcNow;
        var dto = quotes.Select(q => new { Text = _formatter.Format(q), Timestamp = now });
        return Ok(dto);
    }
}
