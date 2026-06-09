using QuotesApi.Models;

namespace QuotesApi.Repositories;

public interface IQuoteRepository
{
    IEnumerable<Quote> GetAll();
}
