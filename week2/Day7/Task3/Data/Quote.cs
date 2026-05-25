namespace QuotesDay7Task3.Data;

public class Quote
{
    public int            Id        { get; set; }
    public int            AuthorId  { get; set; }
    public string         Text      { get; set; } = "";
    public bool           IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Author                Author    { get; set; } = null!;
    public ICollection<QuoteTag> QuoteTags { get; set; } = new List<QuoteTag>();
}
