public record JwtOptions
{
    public string SigningKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = "your-app";
    public string Audience { get; init; } = "your-audience";
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
}
