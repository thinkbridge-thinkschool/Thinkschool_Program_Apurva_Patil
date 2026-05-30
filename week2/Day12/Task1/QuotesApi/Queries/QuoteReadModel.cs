namespace QuotesApi.Queries;

public record QuoteReadModel(
    int Id,
    string Author,
    string Text,
    DateTimeOffset CreatedAt);
