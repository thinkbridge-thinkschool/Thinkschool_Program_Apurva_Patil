using FluentAssertions;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTimestamp()
    {
        // Use a small tolerance window to avoid flaky timing assertions.
        var before = DateTime.UtcNow;
        var sut = new SystemClock();

        var now = sut.UtcNow;
        var after = DateTime.UtcNow;

        now.Kind.Should().Be(DateTimeKind.Utc);
        now.Should().BeOnOrAfter(before);
        now.Should().BeOnOrBefore(after);
    }
}