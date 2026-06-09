/// <summary>
/// Deterministic clock for tests — frozen at a known instant, advanceable by the test.
/// </summary>
public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; }

    public FakeClock(DateTime utcNow) => UtcNow = utcNow;

    public void Advance(TimeSpan by) => UtcNow += by;
}
