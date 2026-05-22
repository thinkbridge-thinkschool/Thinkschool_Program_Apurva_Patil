using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Models;

namespace QuotesApi.Services;

// What this service does:
//   1. MintAccessToken  — creates a short-lived JWT (15 min)
//   2. MintRefreshToken — creates a random long-lived token (7 days)

public interface ITokenService
{
    string MintAccessToken(User user);
    RefreshToken MintRefreshToken(User user);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string MintAccessToken(User user)
    {
        // The secret key — read from config, never hardcoded
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        // HS256 = HMAC SHA-256 — the signing algorithm
        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        // Claims = the data we embed inside the token
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var expiryMinutes = int.Parse(
            _config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        // Serialize the token to the eyJ... string
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken MintRefreshToken(User user)
    {
        var expiryDays = int.Parse(
            _config["Jwt:RefreshTokenExpiryDays"] ?? "7");

        return new RefreshToken
        {
            // 64 random bytes → base64 string. Impossible to guess.
            Token     = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            UserId    = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays),
            
        };
    }
}