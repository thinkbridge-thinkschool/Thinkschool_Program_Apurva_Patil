namespace QuotesDay7Task3.Data;

// Allows an author to belong to multiple categories (classic, modern, etc.)
public class AuthorCategory
{
    public int    AuthorId { get; set; }
    public string Category { get; set; } = "";

    public Author Author { get; set; } = null!;
}
