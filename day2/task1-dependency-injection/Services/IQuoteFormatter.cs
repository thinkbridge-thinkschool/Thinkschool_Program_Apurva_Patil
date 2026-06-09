using QuotesApi.Models;

namespace QuotesApi.Services;

public interface IQuoteFormatter
{
    string Format(Quote q);
}
