namespace QuotesApi.Resilience;

public sealed class ExternalQuoteClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalQuoteClient> _logger;

    public ExternalQuoteClient(HttpClient httpClient, ILogger<ExternalQuoteClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> FetchQuoteAsync(string mode, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling SlowQuoteService with {Mode}", mode);
        var response = await _httpClient.GetAsync(
            $"/external/quote?mode={Uri.EscapeDataString(mode)}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
