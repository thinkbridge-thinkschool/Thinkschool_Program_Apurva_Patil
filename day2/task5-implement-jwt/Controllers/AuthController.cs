using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesJwt.Data;
using QuotesJwt.Models;

namespace QuotesJwt.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(dto.Password ?? string.Empty, user.PasswordHash))
            return Unauthorized();

        var jwtSection = _config.GetSection("Jwt");
        var key = Convert.FromBase64String(jwtSection.GetValue<string>("Key")!);
        var tokenHandler = new JwtSecurityTokenHandler();
        var expiresIn = jwtSection.GetValue<int>("AccessTokenSeconds");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email) }),
            Expires = DateTime.UtcNow.AddSeconds(expiresIn),
            Issuer = jwtSection.GetValue<string>("Issuer"),
            Audience = jwtSection.GetValue<string>("Audience"),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        // Simple refresh token for demo (not persisted)
        var refreshToken = Guid.NewGuid().ToString();

        return Ok(new { access_token = tokenString, refresh_token = refreshToken, expires_in = expiresIn });
    }
}

public record LoginDto(string? Email, string? Password);
