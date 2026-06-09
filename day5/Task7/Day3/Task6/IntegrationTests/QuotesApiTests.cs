using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// End-to-end integration tests that start the full ASP.NET Core pipeline
/// in-process and exercise every auth scenario against real HTTP responses.
/// </summary>
public class QuotesApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public QuotesApiTests(TestWebAppFactory factory)
    {
        _factory = factory;
        // Apply EF migrations once per test class (idempotent).
        _factory.ApplyMigrations();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Mint an InternalJwt token signed with the test key.</summary>
    private static string MakeToken(
        string userId,
        string? scope   = null,
        TimeSpan? lifetime = null)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebAppFactory.TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new("sub", userId) };
        if (scope != null) claims.Add(new("scope", scope));

        var jwt = new JwtSecurityToken(
            issuer:   "your-app",
            audience: "your-audience",
            claims:   claims,
            expires:  DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private HttpClient NewClient() => _factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Test 1: Anonymous → 401 ───────────────────────────────────────────

    [Fact]
    public async Task Anonymous_GetQuotes_Returns401()
    {
        using var client = NewClient();
        var resp = await client.GetAsync("/quotes");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Test 2: Authenticated but wrong policy → 403 ──────────────────────

    [Fact]
    public async Task Authenticated_NoWriteScope_PutQuote_Returns403()
    {
        using var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeToken("user-123")); // no scope

        var resp = await client.PutAsJsonAsync("/quotes/1", "new text");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Test 3: Authenticated + right policy → 200 ────────────────────────

    [Fact]
    public async Task Authenticated_WithWriteScope_PutQuote_Returns200()
    {
        using var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeToken("user-123", scope: "quotes.write"));

        var resp = await client.PutAsJsonAsync("/quotes/1", "updated text");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Test 4: Expired token → 401 ───────────────────────────────────────

    [Fact]
    public async Task ExpiredToken_GetQuotes_Returns401()
    {
        // Token expired 1 hour ago; ClockSkew=0 in the test factory means
        // even 1-second expiry is honoured.
        var expiredToken = MakeToken("user-123", lifetime: TimeSpan.FromHours(-1));

        using var client = NewClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        var resp = await client.GetAsync("/quotes");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Test 5: Revoked refresh chain → 401 ──────────────────────────────

    [Fact]
    public async Task RevokedRefreshChain_Returns401()
    {
        using var client = NewClient();

        // Step A – get initial token pair
        var tokenResp = await client.PostAsJsonAsync("/auth/token",
            new { UserId = "chain-test-user", Scopes = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);

        var first = await tokenResp.Content
            .ReadFromJsonAsync<TokenResponse>(JsonOpts);
        Assert.NotNull(first);

        // Step B – legitimate rotation: old token consumed, new pair issued
        var rotateResp = await client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = first!.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotateResp.StatusCode);

        var second = await rotateResp.Content
            .ReadFromJsonAsync<TokenResponse>(JsonOpts);
        Assert.NotNull(second);

        // Step C – reuse the ORIGINAL (already-consumed) token
        //          → reuse detected, entire family revoked, 401 returned
        var reuseResp = await client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = first!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResp.StatusCode);

        // Step D – the NEW token from Step B is also revoked (family nuked)
        var nukedResp = await client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = second!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, nukedResp.StatusCode);
    }

    // ── shared DTO ────────────────────────────────────────────────────────

    private record TokenResponse(string AccessToken, string RefreshToken);
}
