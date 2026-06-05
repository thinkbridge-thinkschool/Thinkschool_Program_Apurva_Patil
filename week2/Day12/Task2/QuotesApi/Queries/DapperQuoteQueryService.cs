using Dapper;
using Microsoft.Data.SqlClient;

namespace QuotesApi.Queries;

// Reach for Dapper when a read path is BOTH hot AND purely projective:
//   ✓ Hot:       called on nearly every request (paginated list, dashboard feed)
//   ✓ Projective: pure SELECT → DTO, no writes, no change tracking, no business rules
//   ✗ Otherwise: EF Core + AsNoTracking + Select() is good enough and stays in sync with migrations
public class DapperQuoteQueryService : IQuoteQueryService
{
    // The SQL EF Core generates is almost identical — Dapper's edge is that it skips
    // the LINQ→SQL translation layer and DbContext state-machine on every call.
    private const string PagedSql = """
        SELECT Id, Author, Text, CreatedAt
        FROM   Quotes
        WHERE  IsDeleted = 0
        ORDER  BY CreatedAt DESC
        OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY
        """;

    private const string ByIdSql = """
        SELECT Id, Author, Text, CreatedAt
        FROM   Quotes
        WHERE  Id = @id AND IsDeleted = 0
        """;

    private readonly string _connectionString;

    public DapperQuoteQueryService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<List<QuoteReadModel>> GetPagedAsync(
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<QuoteReadModel>(
            new CommandDefinition(PagedSql, new { offset = (page - 1) * size, size }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<QuoteReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<QuoteReadModel>(
            new CommandDefinition(ByIdSql, new { id }, cancellationToken: cancellationToken));
    }
}
