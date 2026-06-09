using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuotesJwt.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    // For demo keep in-memory store
    private static readonly List<object> _store = new();

    [HttpGet]
    public IActionResult Get() => Ok(_store);

    [HttpPost]
    [Authorize]
    public IActionResult Post([FromBody] object dto)
    {
        _store.Add(dto);
        return Ok(dto);
    }

    [HttpDelete]
    [Authorize]
    public IActionResult DeleteAll()
    {
        _store.Clear();
        return NoContent();
    }
}
