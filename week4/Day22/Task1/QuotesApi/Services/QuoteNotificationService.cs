using System.Threading.Channels;

namespace QuotesApi.Services;

public class QuoteNotificationService : BackgroundService
{
    private readonly Channel<int> _channel;
    private readonly ILogger<QuoteNotificationService> _logger;

    public QuoteNotificationService(Channel<int> channel, ILogger<QuoteNotificationService> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                var quoteId = await _channel.Reader.ReadAsync(stoppingToken);
                try
                {
                    _logger.LogInformation("Sending notification for quote {Id}", quoteId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process notification for quote {Id}", quoteId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("QuoteNotificationService stopping.");
        }
    }
}
