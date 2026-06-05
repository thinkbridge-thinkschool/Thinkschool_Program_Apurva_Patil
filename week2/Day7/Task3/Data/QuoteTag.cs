namespace QuotesDay7Task3.Data;

public class QuoteTag
{
    public int QuoteId { get; set; }
    public int TagId   { get; set; }

    public Quote Quote { get; set; } = null!;
    public Tag   Tag   { get; set; } = null!;
}
