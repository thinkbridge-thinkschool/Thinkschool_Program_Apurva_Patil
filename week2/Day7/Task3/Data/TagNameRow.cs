using Microsoft.EntityFrameworkCore;

namespace QuotesDay7Task3.Data;

// Keyless projection — maps the single-column TagName result from the UNION query
[Keyless]
public class TagNameRow
{
    public string TagName { get; set; } = "";
}
