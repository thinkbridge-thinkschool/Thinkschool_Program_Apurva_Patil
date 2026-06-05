using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

// Request/response shapes
record RegisterRequest(string Email, string Password);
record LoginRequest(string Email, string Password);
record RefreshRequest(string RefreshToken);
record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn);

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithName("Auth");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithDescription("Create a new user account");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithDescription("Login and receive access + refresh tokens");

        group.MapPost("/refresh", Refresh)
            .WithName("Refresh")
            .WithDescription("Exchange a refresh token for a new access token");

        return app;
    }

    // ── POST /api/auth/register ───────────────────────────────────────────
    // Body: { "email": "raj@test.com", "password": "secret123" }
    private static async Task<IResult> Register(
        RegisterRequest request,
        QuotesDbContext db,
        CancellationToken cancellationToken)
    {
        // Check if email already taken
        var exists = await db.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (exists)
            return Results.Conflict(new { error = "Email already registered." });

        // User.Create hashes the password via BCrypt
        var (success, user, error) = User.Create(request.Email, request.Password);

        if (!success)
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["user"] = new[] { error! }
                });

        db.Users.Add(user!);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/auth/register", new { user!.Id, user.Email });
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────
    // Body: { "email": "raj@test.com", "password": "secret123" }
    // Returns: { "access_token": "eyJ...", "refresh_token": "abc...", "expires_in": 900 }
    private static async Task<IResult> Login(
        LoginRequest request,
        QuotesDbContext db,
        ITokenService tokenService,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        // Find user by email
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        // VerifyPassword uses BCrypt to check plain text against the hash
        if (user is null || !user.VerifyPassword(request.Password))
            return Results.Unauthorized();

        // Mint the JWT access token (15 min)
        var accessToken = tokenService.MintAccessToken(user);

        // Mint the refresh token (7 days) and save to DB
        var refreshToken = tokenService.MintRefreshToken(user);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(cancellationToken);

        var expiryMinutes = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        return Results.Ok(new AuthResponse(
            AccessToken:  accessToken,
            RefreshToken: refreshToken.Token,
            ExpiresIn:    expiryMinutes * 60   // seconds
        ));
    }

    // ── POST /api/auth/refresh ────────────────────────────────────────────
    // Body: { "refreshToken": "abc..." }
    // Returns a brand new access token + rotated refresh token
    private static async Task<IResult> Refresh(
        RefreshRequest request,
        QuotesDbContext db,
        ITokenService tokenService,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        // Find the refresh token in DB, include the User
        var stored = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, cancellationToken);

        // Reject if not found, already revoked, or expired
    if (stored is null)
{
    return Results.Unauthorized();
}

if (stored.IsRevoked)
{
    Console.WriteLine("SECURITY EVENT: Refresh token reuse detected.");

    var family = await db.RefreshTokens
        .Where(r => r.UserId == stored.UserId)
        .ToListAsync(cancellationToken);

    foreach (var token in family)
    {
        token.RevokedAt = DateTimeOffset.UtcNow;
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Unauthorized();
}

if (stored.IsExpired)
{
    return Results.Unauthorized();
}

        // Rotate — revoke old token, issue new one
       stored.RevokedAt = DateTimeOffset.UtcNow;

        var newRefresh = tokenService.MintRefreshToken(stored.User);
        stored.ReplacedByToken = newRefresh.Token;

        db.RefreshTokens.Add(newRefresh);
        await db.SaveChangesAsync(cancellationToken);

        var accessToken    = tokenService.MintAccessToken(stored.User);
        var expiryMinutes  = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        return Results.Ok(new AuthResponse(
            AccessToken:  accessToken,
            RefreshToken: newRefresh.Token,
            ExpiresIn:    expiryMinutes * 60
        ));
    }
}