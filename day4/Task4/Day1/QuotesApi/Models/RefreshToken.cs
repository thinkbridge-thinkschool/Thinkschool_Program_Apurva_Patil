namespace QuotesApi.Models;

public class RefreshToken
{
    public int Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public int UserId { get; set; }

    public User User { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    public bool IsExpired =>
        DateTimeOffset.UtcNow >= ExpiresAt;

    public bool IsRevoked =>
        RevokedAt is not null;

    public bool IsValid =>
        !IsExpired && !IsRevoked;
}