using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

public class TokenServiceTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-chars!!";
    private static readonly DateTime FixedUtcNow =
        new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private static TokenService CreateSut(FakeClock clock)
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<JwtOptions>>();
        optionsMonitor.CurrentValue.Returns(new JwtOptions
        {
            SigningKey = SigningKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenLifetime = TimeSpan.FromMinutes(15)
        });

        return new TokenService(optionsMonitor, clock);
    }

    // ─── IssueRefreshToken ───────────────────────────────────────────────────

    [Fact]
    public void IssueRefreshToken_ValidUser_ReturnsTokenWithCorrectUserId()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);

        // Act
        var rt = sut.IssueRefreshToken("user-123");

        // Assert
        rt.UserId.Should().Be("user-123");
    }

    [Fact]
    public void IssueRefreshToken_DefaultExpiry_IsSevenDaysFromClock()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);

        // Act
        var rt = sut.IssueRefreshToken("user-123");

        // Assert — expiry must be exactly 7 days from the frozen clock instant
        rt.ExpiresAt.Should().Be(FixedUtcNow.AddDays(7));
    }

    [Fact]
    public void IssueRefreshToken_WithExplicitFamilyId_UsesThatFamily()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);
        const string familyId = "my-known-family";

        // Act
        var rt = sut.IssueRefreshToken("user-123", familyId);

        // Assert
        rt.FamilyId.Should().Be(familyId);
    }

    [Fact]
    public void IssueRefreshToken_WithoutFamilyId_AssignsNewFamily()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);

        // Act
        var rt1 = sut.IssueRefreshToken("user-123");
        var rt2 = sut.IssueRefreshToken("user-123");

        // Assert — different tokens should get different auto-assigned families
        rt1.FamilyId.Should().NotBe(rt2.FamilyId);
    }

    // ─── RotateRefreshToken — happy path ─────────────────────────────────────

    [Fact]
    public void RotateRefreshToken_ValidToken_ReturnsNewTokenPair()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);
        var rt = sut.IssueRefreshToken("user-123");

        // Act
        var result = sut.RotateRefreshToken(rt.Token);

        // Assert
        result.Should().NotBeNull();
        result!.Value.accessToken.Should().NotBeNullOrEmpty();
        result.Value.refreshToken.Token.Should().NotBe(rt.Token);
    }

    [Fact]
    public void RotateRefreshToken_ValidToken_NewRefreshTokenSharesSameFamily()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);
        var rt = sut.IssueRefreshToken("user-123");
        var originalFamily = rt.FamilyId;

        // Act
        var result = sut.RotateRefreshToken(rt.Token);

        // Assert — rotation keeps the same family chain
        result!.Value.refreshToken.FamilyId.Should().Be(originalFamily);
    }

    // ─── RotateRefreshToken — rejection paths ────────────────────────────────

    [Fact]
    public void RotateRefreshToken_UnknownToken_ReturnsNull()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);

        // Act
        var result = sut.RotateRefreshToken("this-token-was-never-issued");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RotateRefreshToken_RevokedToken_ReturnsNull()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);
        var rt = sut.IssueRefreshToken("user-123");
        rt.IsRevoked = true;  // externally mark as revoked

        // Act
        var result = sut.RotateRefreshToken(rt.Token);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RotateRefreshToken_ExpiredToken_ReturnsNull()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);
        var rt = sut.IssueRefreshToken("user-123");

        // Advance past the 7-day expiry window
        clock.Advance(TimeSpan.FromDays(8));

        // Act
        var result = sut.RotateRefreshToken(rt.Token);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RotateRefreshToken_AfterSuccessfulRotation_OldTokenIsConsumedAndReturnsNull()
    {
        // Arrange
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);
        var rt = sut.IssueRefreshToken("user-123");
        sut.RotateRefreshToken(rt.Token);  // first (valid) rotation consumes rt

        // Act — replay the same token
        var secondAttempt = sut.RotateRefreshToken(rt.Token);

        // Assert — consumed token must not be accepted
        secondAttempt.Should().BeNull();
    }

    // ─── RotateRefreshToken — reuse / theft detection ────────────────────────

    [Fact]
    public void RotateRefreshToken_ReuseDetected_RevokesSuccessorAndReturnsNull()
    {
        // Arrange — issue rt1, rotate to rt2, then replay rt1 (reuse attack)
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);

        var rt1 = sut.IssueRefreshToken("user-123");
        var rotated = sut.RotateRefreshToken(rt1.Token);  // valid rotation
        var rt2 = rotated!.Value.refreshToken;

        // Act — attacker replays the already-used rt1
        var attackResult = sut.RotateRefreshToken(rt1.Token);

        // Assert — reuse detected, no new pair issued
        attackResult.Should().BeNull();

        // The successor (rt2) must also be revoked so the attacker cannot
        // continue using the chain even if they obtained rt2 later.
        var rt2Attempt = sut.RotateRefreshToken(rt2.Token);
        rt2Attempt.Should().BeNull();
    }

    [Fact]
    public void RotateRefreshToken_ReuseDetected_RevokesEntireFamilyNotJustOneToken()
    {
        // Arrange — build a chain: rt1 → rt2 → rt3, then replay rt1
        var clock = new FakeClock(FixedUtcNow);
        var sut = CreateSut(clock);

        var rt1 = sut.IssueRefreshToken("user-123");
        var rt2 = sut.RotateRefreshToken(rt1.Token)!.Value.refreshToken;
        var rt3 = sut.RotateRefreshToken(rt2.Token)!.Value.refreshToken;

        // Act — replay rt1 (oldest, already used twice over)
        sut.RotateRefreshToken(rt1.Token);

        // Assert — rt3 (the most recently issued in the chain) is also revoked
        var rt3Attempt = sut.RotateRefreshToken(rt3.Token);
        rt3Attempt.Should().BeNull();
    }
}
