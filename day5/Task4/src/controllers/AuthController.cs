using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[AllowAnonymous]
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokens;

    public AuthController(ITokenService tokens) => _tokens = tokens;

    // POST /auth/token — issues access + refresh token for a given userId.
    [HttpPost("token")]
    public IActionResult Token([FromBody] TokenRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.UserId))
            return BadRequest(new { error = "userId required" });

        var access  = _tokens.IssueAccessToken(req.UserId, req.Scopes ?? []);
        var refresh = _tokens.IssueRefreshToken(req.UserId);

        return Ok(new { accessToken = access, refreshToken = refresh.Token });
    }

    // POST /auth/refresh — rotates a refresh token; revokes family on reuse.
    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] RefreshRequest req)
    {
        var result = _tokens.RotateRefreshToken(req.RefreshToken);
        if (result is null)
            return Unauthorized(new { error = "invalid_grant" });

        return Ok(new
        {
            accessToken  = result.Value.accessToken,
            refreshToken = result.Value.refreshToken.Token
        });
    }
}

public record TokenRequest(string UserId, string[]? Scopes);
public record RefreshRequest(string RefreshToken);
