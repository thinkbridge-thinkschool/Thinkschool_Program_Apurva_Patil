namespace QuotesDay7Task2.Data;

public class Quote
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public string Text { get; set; } = "";
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Author Author { get; set; } = null!;
}
