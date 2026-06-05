using Microsoft.EntityFrameworkCore;

namespace QuotesDay7Task2.Data;

// Keyless entity — maps the window-function CTE result; never written to a table
[Keyless]
public class QuoteWindowRow
{
    public string AuthorName { get; set; } = "";
    public int QuoteId { get; set; }
    public int RowNum { get; set; }
    public int Rnk { get; set; }
    public int RunningTotal { get; set; }
    public int? DaysSincePrevious { get; set; }
    public string QuoteText { get; set; } = "";
}
