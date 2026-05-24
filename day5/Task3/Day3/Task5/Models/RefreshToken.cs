public class RefreshToken
{
    public string Token { get; init; } = "";
    public string UserId { get; init; } = "";
    // All tokens in the same rotation chain share a FamilyId.
    // If any member is reused, the entire family is revoked.
    public string FamilyId { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
    public bool IsRevoked { get; set; }
    public bool IsUsed { get; set; }
}
