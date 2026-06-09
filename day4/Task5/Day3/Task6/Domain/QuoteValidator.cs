public static class QuoteValidator
{
    public const int MaxTextLength = 500;
    public const int MaxOwnerIdLength = 100;

    public static IReadOnlyList<string> Validate(int id, string? ownerId, string? text)
    {
        var errors = new List<string>();

        if (id <= 0)
            errors.Add("Id must be greater than zero.");

        if (string.IsNullOrWhiteSpace(ownerId))
            errors.Add("OwnerId is required.");
        else if (ownerId.Length > MaxOwnerIdLength)
            errors.Add($"OwnerId must be {MaxOwnerIdLength} characters or fewer.");

        if (string.IsNullOrWhiteSpace(text))
            errors.Add("Text is required.");
        else if (text.Length > MaxTextLength)
            errors.Add($"Text must be {MaxTextLength} characters or fewer.");

        return errors.AsReadOnly();
    }
}
