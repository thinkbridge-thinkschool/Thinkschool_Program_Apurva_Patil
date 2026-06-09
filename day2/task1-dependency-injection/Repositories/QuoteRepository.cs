using QuotesApi.Models;

namespace QuotesApi.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private static readonly List<Quote> _quotes = new()
    {
        new Quote { Id = 1, Text = "The only limit is your imagination." },
        new Quote { Id = 2, Text = "Simplicity is the soul of efficiency." }
    };

    public IEnumerable<Quote> GetAll() => _quotes;
}
