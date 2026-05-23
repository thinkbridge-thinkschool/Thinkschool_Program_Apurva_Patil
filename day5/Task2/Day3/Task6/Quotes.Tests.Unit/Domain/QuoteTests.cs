using FluentAssertions;
using NSubstitute;

public class QuoteTests
{
    private static readonly DateTime FixedUtcNow =
        new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    // ─── Success paths ───────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInputs_IsSuccessIsTrue()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(1, "user-123", "To be or not to be.", clock);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void Create_ValidInputs_SetsAllProperties()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(42, "user-999", "Hello world.", clock);

        // Assert
        result.Value!.Id.Should().Be(42);
        result.Value.OwnerId.Should().Be("user-999");
        result.Value.Text.Should().Be("Hello world.");
    }

    [Fact]
    public void Create_ValidInputs_SetsCreatedAtFromClock()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(1, "user-123", "Some quote.", clock);

        // Assert
        result.Value!.CreatedAt.Should().Be(FixedUtcNow);
    }

    [Fact]
    public void Create_ValidInputs_TrimsOwnerIdAndText()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(1, "  user-123  ", "  Trimmed text.  ", clock);

        // Assert
        result.Value!.OwnerId.Should().Be("user-123");
        result.Value.Text.Should().Be("Trimmed text.");
    }

    // ─── Failure paths: Id ───────────────────────────────────────────────────

    [Fact]
    public void Create_IdIsZero_ReturnsFailure()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(0, "user-123", "Some text.", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("Id"));
    }

    [Fact]
    public void Create_IdIsNegative_ReturnsFailure()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(-5, "user-123", "Some text.", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("Id"));
    }

    // ─── Failure paths: OwnerId ──────────────────────────────────────────────

    [Fact]
    public void Create_NullOwnerId_ReturnsFailure()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(1, null, "Some text.", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("OwnerId"));
    }

    [Fact]
    public void Create_OwnerIdTooLong_ReturnsFailure()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);
        var longOwnerId = new string('x', QuoteValidator.MaxOwnerIdLength + 1);

        // Act
        var result = Quote.Create(1, longOwnerId, "Some text.", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("OwnerId"));
    }

    // ─── Failure paths: Text ─────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyText_ReturnsFailure()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(1, "user-123", "", clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("Text"));
    }

    [Fact]
    public void Create_TextTooLong_ReturnsFailure()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);
        var longText = new string('a', QuoteValidator.MaxTextLength + 1);

        // Act
        var result = Quote.Create(1, "user-123", longText, clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("Text"));
    }

    // ─── Failure paths: parameterized ────────────────────────────────────────

    [Theory]
    [InlineData(0,   "user-123", "Some text.")]   // bad id
    [InlineData(1,   null,       "Some text.")]   // null ownerId
    [InlineData(1,   "",         "Some text.")]   // empty ownerId
    [InlineData(1,   "user-123", null          )] // null text
    [InlineData(1,   "user-123", ""            )] // empty text
    public void Create_AnyInvalidField_IsSuccessIsFalse(int id, string? ownerId, string? text)
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedUtcNow);

        // Act
        var result = Quote.Create(id, ownerId, text, clock);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_InvalidInputs_DoesNotCallClock()
    {
        // Arrange — if validation fails the clock should never be read
        var clock = Substitute.For<IClock>();

        // Act
        Quote.Create(0, null, null, clock);

        // Assert — UtcNow was never accessed
        _ = clock.DidNotReceive().UtcNow;
    }
}
