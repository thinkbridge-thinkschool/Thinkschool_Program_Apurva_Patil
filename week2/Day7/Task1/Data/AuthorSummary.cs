using Microsoft.EntityFrameworkCore;

namespace QuotesDay7.Data;

// Keyless entity — used only to map the CTE result set, never written to a table
[Keyless]
public class AuthorSummary
{
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = "";
    public int QuoteCount { get; set; }
    public string? MostRecentQuoteText { get; set; }
}
