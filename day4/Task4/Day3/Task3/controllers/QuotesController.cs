using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]                          // All endpoints require authentication
[ApiController]
[Route("[controller]")]
public class QuotesController : ControllerBase
{
    // GET /quotes — any authenticated user
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new[]
        {
            new { Id = 1, OwnerId = "user-123", Text = "To be or not to be." },
            new { Id = 2, OwnerId = "user-456", Text = "All that glitters is not gold." }
        });
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
