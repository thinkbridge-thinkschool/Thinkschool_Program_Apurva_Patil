namespace QuotesDay7Task3.Data;

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Bio { get; set; }

    public ICollection<Quote>          Quotes     { get; set; } = new List<Quote>();
    public ICollection<AuthorCategory> Categories { get; set; } = new List<AuthorCategory>();
}
