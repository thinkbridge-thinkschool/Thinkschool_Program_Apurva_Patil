namespace QuotesApi.Services;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }

    public FakeClock(DateTimeOffset now)
    {
        UtcNow = now;
    }
}
