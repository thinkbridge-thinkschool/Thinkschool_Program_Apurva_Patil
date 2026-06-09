using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly QuotesDbContext _context;
    private readonly ILogger<QuoteRepository> _logger;

    public QuoteRepository(QuotesDbContext context, ILogger<QuoteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Quote>> GetAllAsync(
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching quotes: page {Page}, size {Size}", page, size);

        return await _context.Quotes
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    public async Task<Quote?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching quote with ID {Id}", id);

        return await _context.Quotes
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<Quote> AddAsync(
        Quote quote,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding new quote by {Author}", quote.Author);

        _context.Quotes.Add(quote);
        await _context.SaveChangesAsync(cancellationToken);

        return quote;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting quote with ID {Id}", id);

        var quote = await _context.Quotes
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quote is null)
        {
            _logger.LogWarning("Quote with ID {Id} not found", id);
            return false;
        }

        _context.Quotes.Remove(quote);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}