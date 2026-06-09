using QuotesApi.Models;

namespace QuotesApi.Services;

public class QuoteFormatter : IQuoteFormatter
{
    public string Format(Quote q) => $"#{q.Id}: {q.Text}";
}
