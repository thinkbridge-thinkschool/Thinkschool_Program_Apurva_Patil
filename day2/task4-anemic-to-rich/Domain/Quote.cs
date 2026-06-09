using System;

namespace Task04.Riches.Domain;

public sealed class Quote
{
    public int Id { get; private set; }
    public string Author { get; private set; }
    public string Text { get; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Quote(string author, string text)
    {
        Author = author;
        Text = text;
        IsDeleted = false;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // Factory enforces invariants and returns a domain error string on failure.
    public static Result<Quote> Create(string author, string text)
    {
        if (string.IsNullOrWhiteSpace(author))
            return Result<Quote>.Failure("Author must not be empty");
        if (author.Length > 200)
            return Result<Quote>.Failure("Author must be at most 200 characters");
        if (string.IsNullOrWhiteSpace(text))
            return Result<Quote>.Failure("Text must not be empty");
        if (text.Length > 1000)
            return Result<Quote>.Failure("Text must be at most 1000 characters");

        var q = new Quote(author.Trim(), text.Trim());
        return Result<Quote>.Success(q);
    }

    // Text is immutable after creation. Allow soft-delete only.
    public void SoftDelete()
    {
        IsDeleted = true;
    }

    // Allow changing author but not the text.
    public void ChangeAuthor(string newAuthor)
    {
        if (string.IsNullOrWhiteSpace(newAuthor))
            throw new ArgumentException("Author must not be empty", nameof(newAuthor));
        if (newAuthor.Length > 200)
            throw new ArgumentException("Author too long", nameof(newAuthor));
        Author = newAuthor.Trim();
    }
}
