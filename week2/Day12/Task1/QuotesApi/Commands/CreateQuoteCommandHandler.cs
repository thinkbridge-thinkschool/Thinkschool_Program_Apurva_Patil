using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Commands;

public class CreateQuoteCommandHandler
{
    private readonly IQuoteRepository _repository;

    public CreateQuoteCommandHandler(IQuoteRepository repository)
    {
        _repository = repository;
    }

    public async Task<(int? QuoteId, Dictionary<string, string[]>? Errors)> HandleAsync(
        CreateQuoteCommand command,
        CancellationToken cancellationToken)
    {
        var (success, quote, error) = Quote.Create(command.Author, command.Text);

        if (!success)
        {
            return (null, new Dictionary<string, string[]>
            {
                ["quote"] = new[] { error! }
            });
        }

        var created = await _repository.AddAsync(quote!, cancellationToken);
        return (created.Id, null);
    }
}
