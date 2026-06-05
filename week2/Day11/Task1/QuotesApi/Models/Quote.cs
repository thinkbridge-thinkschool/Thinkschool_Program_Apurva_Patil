namespace QuotesApi.Models;

public class Quote
{
    private Quote()
    {
    }

    private Quote(string author, string text)
    {
        Author = author;
        Text = text;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public int Id { get; private set; }

    public string Author { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static (bool Success, Quote? Quote, string? Error)
        Create(string author, string text)
    {
        if (string.IsNullOrWhiteSpace(author) ||
            author.Length > 200)
        {
            return (
                false,
                null,
                "Author must be between 1 and 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(text) ||
            text.Length > 1000)
        {
            return (
                false,
                null,
                "Text must be between 1 and 1000 characters.");
        }

        return (
            true,
            new Quote(author, text),
            null);
    }

    public void SoftDelete()
    {
        IsDeleted = true;
    }
}
