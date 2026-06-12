namespace QuotesApi.Models;

public class ProcessedMessage
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
