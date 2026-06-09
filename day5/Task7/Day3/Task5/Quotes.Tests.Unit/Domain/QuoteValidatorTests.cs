using FluentAssertions;

public class QuoteValidatorTests
{
    // ─── Valid input ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        // Arrange
        int id = 1;
        string ownerId = "user-123";
        string text = "To be or not to be.";

        // Act
        var errors = QuoteValidator.Validate(id, ownerId, text);

        // Assert
        errors.Should().BeEmpty();
    }

    // ─── Id validation ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void Validate_IdIsNotPositive_ReturnsIdError(int invalidId)
    {
        // Arrange
        string ownerId = "user-123";
        string text = "Valid text.";

        // Act
        var errors = QuoteValidator.Validate(invalidId, ownerId, text);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("Id"));
    }

    [Fact]
    public void Validate_IdIsOne_DoesNotReturnIdError()
    {
        // Arrange & Act
        var errors = QuoteValidator.Validate(1, "user-123", "Some text.");

        // Assert
        errors.Should().NotContain(e => e.Contains("Id"));
    }

    // ─── OwnerId validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_OwnerIdIsNullOrWhitespace_ReturnsOwnerIdError(string? ownerId)
    {
        // Act
        var errors = QuoteValidator.Validate(1, ownerId, "Some text.");

        // Assert
        errors.Should().ContainSingle(e => e.Contains("OwnerId"));
    }

    [Fact]
    public void Validate_OwnerIdExceedsMaxLength_ReturnsOwnerIdError()
    {
        // Arrange
        var longOwnerId = new string('x', QuoteValidator.MaxOwnerIdLength + 1);

        // Act
        var errors = QuoteValidator.Validate(1, longOwnerId, "Some text.");

        // Assert
        errors.Should().ContainSingle(e => e.Contains("OwnerId"));
    }

    [Fact]
    public void Validate_OwnerIdAtMaxLength_ReturnsNoOwnerIdError()
    {
        // Arrange — exactly at the boundary: valid
        var ownerIdAtLimit = new string('x', QuoteValidator.MaxOwnerIdLength);

        // Act
        var errors = QuoteValidator.Validate(1, ownerIdAtLimit, "Some text.");

        // Assert
        errors.Should().NotContain(e => e.Contains("OwnerId"));
    }

    // ─── Text validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_TextIsNullOrWhitespace_ReturnsTextError(string? text)
    {
        // Act
        var errors = QuoteValidator.Validate(1, "user-123", text);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("Text"));
    }

    [Fact]
    public void Validate_TextExceedsMaxLength_ReturnsTextError()
    {
        // Arrange
        var longText = new string('a', QuoteValidator.MaxTextLength + 1);

        // Act
        var errors = QuoteValidator.Validate(1, "user-123", longText);

        // Assert
        errors.Should().ContainSingle(e => e.Contains("Text"));
    }

    [Fact]
    public void Validate_TextAtMaxLength_ReturnsNoTextError()
    {
        // Arrange — exactly at the boundary: valid
        var textAtLimit = new string('a', QuoteValidator.MaxTextLength);

        // Act
        var errors = QuoteValidator.Validate(1, "user-123", textAtLimit);

        // Assert
        errors.Should().NotContain(e => e.Contains("Text"));
    }

    // ─── Multiple errors ─────────────────────────────────────────────────────

    [Fact]
    public void Validate_AllFieldsInvalid_ReturnsThreeErrors()
    {
        // Arrange — every field fails: id ≤ 0, ownerId null, text null
        int id = -1;
        string? ownerId = null;
        string? text = null;

        // Act
        var errors = QuoteValidator.Validate(id, ownerId, text);

        // Assert
        errors.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(0,   "user-123", "Valid text.")]  // only id invalid
    [InlineData(1,   null,       "Valid text.")]  // only ownerId invalid
    [InlineData(1,   "user-123", null          )] // only text invalid
    public void Validate_SingleFieldInvalid_ReturnsOneError(int id, string? ownerId, string? text)
    {
        // Act
        var errors = QuoteValidator.Validate(id, ownerId, text);

        // Assert
        errors.Should().HaveCount(1);
    }
}
