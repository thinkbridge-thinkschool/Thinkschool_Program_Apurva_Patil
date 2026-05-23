using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public interface ITokenService
{
    string IssueAccessToken(string userId, string[] scopes, TimeSpan? lifetime = null);
    RefreshToken IssueRefreshToken(string userId, string? familyId = null);
    (string accessToken, RefreshToken refreshToken)? RotateRefreshToken(string incomingToken);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly IClock _clock;

    // Instance store (TokenService is registered as singleton, so one store per app lifetime).
    // Instance field makes unit tests independent — each test gets a fresh store.
    private readonly Dictionary<string, RefreshToken> _store = new();
    private readonly object _lock = new();

    public TokenService(IConfiguration config, IClock clock)
    {
        _config = config;
        _clock = clock;
    }

    public string IssueAccessToken(string userId, string[] scopes, TimeSpan? lifetime = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:SigningKey"]!));

        var claims = new List<Claim> { new("sub", userId) };
        if (scopes.Length > 0)
            claims.Add(new Claim("scope", string.Join(" ", scopes)));

        var token = new JwtSecurityToken(
            issuer: "your-app",
            audience: "your-audience",
            claims: claims,
            expires: _clock.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken IssueRefreshToken(string userId, string? familyId = null)
    {
        var rt = new RefreshToken
        {
            Token    = Guid.NewGuid().ToString("N"),
            UserId   = userId,
            FamilyId = familyId ?? Guid.NewGuid().ToString("N"),
            ExpiresAt = _clock.UtcNow.AddDays(7)
        };
        lock (_lock) _store[rt.Token] = rt;
        return rt;
    }

    public (string accessToken, RefreshToken refreshToken)? RotateRefreshToken(string incomingToken)
    {
        string userId, familyId;

        lock (_lock)
        {
            if (!_store.TryGetValue(incomingToken, out var rt))
                return null;

            // Reuse detected: this token was already consumed → revoke every token
            // in the family so a stolen token cannot be used anywhere in the chain.
            if (rt.IsUsed)
            {
                foreach (var t in _store.Values)
                    if (t.FamilyId == rt.FamilyId) t.IsRevoked = true;
                return null;
            }

            if (rt.IsRevoked || rt.ExpiresAt < _clock.UtcNow)
                return null;

            rt.IsUsed = true;          // consume before issuing successor
            userId   = rt.UserId;
            familyId = rt.FamilyId;
        }

        var newAccess = IssueAccessToken(userId, ["quotes.write"]);
        var newRt     = IssueRefreshToken(userId, familyId);   // same family
        return (newAccess, newRt);
    }
}
