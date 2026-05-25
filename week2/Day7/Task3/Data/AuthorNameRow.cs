using Microsoft.EntityFrameworkCore;

namespace QuotesDay7Task3.Data;

// Keyless projection — maps (AuthorId, AuthorName) results from set-operation queries
[Keyless]
public class AuthorNameRow
{
    public int    AuthorId   { get; set; }
    public string AuthorName { get; set; } = "";
}
