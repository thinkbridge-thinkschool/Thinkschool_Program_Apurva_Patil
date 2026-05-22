public class Quote
{
    public int Id { get; init; }
    public string OwnerId { get; init; } = "";
    public string Text { get; init; } = "";
    public DateTime CreatedAt { get; init; }

    private Quote() { }

    public static Result<Quote> Create(int id, string? ownerId, string? text, IClock clock)
    {
        var errors = QuoteValidator.Validate(id, ownerId, text);
        if (errors.Count > 0)
            return Result<Quote>.Failure(errors);

        return Result<Quote>.Success(new Quote
        {
            Id = id,
            OwnerId = ownerId!.Trim(),
            Text = text!.Trim(),
            CreatedAt = clock.UtcNow
        });
    }
}
