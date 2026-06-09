public class QuoteEntity
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
