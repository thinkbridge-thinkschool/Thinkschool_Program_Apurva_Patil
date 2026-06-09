using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly QuotesDbContext _context;
    private readonly ILogger<CollectionRepository> _logger;

    public CollectionRepository(QuotesDbContext context, ILogger<CollectionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Collection?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching collection with ID {Id}", id);

        // Owned entities (Items) are automatically loaded with the owner
        return await _context.Collections
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Collection> AddAsync(Collection collection, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding new collection '{Name}'", collection.Name);

        _context.Collections.Add(collection);
        await _context.SaveChangesAsync(cancellationToken);

        return collection;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
