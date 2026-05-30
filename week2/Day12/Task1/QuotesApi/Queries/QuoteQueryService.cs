using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Queries;

public class QuoteQueryService : IQuoteQueryService
{
    private readonly QuotesDbContext _db;

    public QuoteQueryService(QuotesDbContext db)
    {
        _db = db;
    }

    public async Task<List<QuoteReadModel>> GetPagedAsync(
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        return await _db.Quotes
            .Where(q => !q.IsDeleted)
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .AsNoTracking()
            .Select(q => new QuoteReadModel(q.Id, q.Author, q.Text, q.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<QuoteReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.Quotes
            .Where(q => q.Id == id && !q.IsDeleted)
            .AsNoTracking()
            .Select(q => new QuoteReadModel(q.Id, q.Author, q.Text, q.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
