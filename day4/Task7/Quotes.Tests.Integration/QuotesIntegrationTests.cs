using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// Full integration test suite for the Quotes API.
///
/// Isolation: xUnit creates a NEW class instance per test method, so each
/// test gets its own IntegrationTestFactory, its own in-memory SQLite DB
/// (via the open keep-alive SqliteConnection), and its own HttpClient.
/// No state is shared between tests.
/// </summary>
public class QuotesIntegrationTests : IDisposable
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public QuotesIntegrationTests()
    {
        _factory = new IntegrationTestFactory();
        _client  = _factory.CreateClient();
        _factory.ApplyMigrations();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Mint an InternalJwt token signed with the test key.</summary>
    private static string MakeToken(
        string userId,
        string? scope    = null,
        TimeSpan? lifetime = null)
    {
        var key   = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(IntegrationTestFactory.TestSigningKey));
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

    private void AuthAs(string userId, string? scope = null) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MakeToken(userId, scope));

    // ── AUTH CONTROLLER ───────────────────────────────────────────────────────

    // Test 1
    [Fact]
    public async Task PostToken_ValidUserId_Returns200WithTokenPair()
    {
        var resp = await _client.PostAsJsonAsync("/auth/token",
            new { UserId = "alice", Scopes = Array.Empty<string>() });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    // Test 2
    [Fact]
    public async Task PostToken_EmptyUserId_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/auth/token",
            new { UserId = "", Scopes = Array.Empty<string>() });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Test 3
    [Fact]
    public async Task PostRefresh_ValidToken_Returns200WithNewPair()
    {
        // Issue initial pair
        var first = await (await _client.PostAsJsonAsync("/auth/token",
            new { UserId = "bob", Scopes = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);

        // Rotate
        var resp = await _client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = first!.RefreshToken });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await resp.Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);
        second!.AccessToken.Should().NotBeNullOrEmpty();
        second.RefreshToken.Should().NotBe(first.RefreshToken, "rotation must yield a new token");
    }

    // Test 4
    [Fact]
    public async Task PostRefresh_InvalidToken_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = "not-a-real-token" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── QUOTES CONTROLLER — unauthenticated paths ─────────────────────────────

    // Test 5
    [Fact]
    public async Task GetQuotes_NoAuth_Returns401()
    {
        var resp = await _client.GetAsync("/quotes");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Test 6
    [Fact]
    public async Task GetQuotes_ExpiredToken_Returns401()
    {
        // Token whose expiry is 1 hour in the past; ClockSkew=0 in factory
        var expired = MakeToken("user-x", lifetime: TimeSpan.FromHours(-1));
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expired);

        var resp = await _client.GetAsync("/quotes");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── QUOTES CONTROLLER — authenticated happy paths ─────────────────────────

    // Test 7
    [Fact]
    public async Task GetQuotes_Authenticated_ReturnsEmptyListOnFreshDb()
    {
        AuthAs("user-1");
        var resp = await _client.GetAsync("/quotes");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<QuoteEntity[]>(JsonOpts);
        list.Should().BeEmpty("fresh DB has no quotes");
    }

    // Test 8
    [Fact]
    public async Task GetQuotes_Authenticated_ReturnsSeededData()
    {
        // Seed directly through the DB helper (bypasses HTTP for speed)
        _factory.SeedQuote("owner-1", "First quote");
        _factory.SeedQuote("owner-2", "Second quote");

        AuthAs("reader");
        var resp = await _client.GetAsync("/quotes");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.Content.ReadFromJsonAsync<QuoteEntity[]>(JsonOpts);
        list.Should().HaveCount(2);
    }

    // Test 9
    [Fact]
    public async Task PostQuote_Valid_Returns201WithLocation()
    {
        AuthAs("author-1");
        var resp = await _client.PostAsJsonAsync("/quotes", new { Text = "To be or not to be." });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        resp.Headers.Location.Should().NotBeNull("a Created response must include Location");

        var body = await resp.Content.ReadFromJsonAsync<QuoteEntity>(JsonOpts);
        body!.Text.Should().Be("To be or not to be.");
        body.OwnerId.Should().Be("author-1");
    }

    // Test 10 — validation → ProblemDetails
    [Fact]
    public async Task PostQuote_EmptyText_Returns400ProblemDetails()
    {
        AuthAs("author-2");
        // [Required(AllowEmptyStrings = false)] on CreateQuoteRequest.Text should
        // trigger automatic 400 + ProblemDetails via [ApiController].
        var resp = await _client.PostAsJsonAsync("/quotes", new { Text = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("errors", "ProblemDetails response must include 'errors'");
    }

    // Test 11 — validation → ProblemDetails for null text
    [Fact]
    public async Task PostQuote_NullText_Returns400ProblemDetails()
    {
        AuthAs("author-3");
        // Send JSON with Text=null — [Required] should reject it
        var resp = await _client.PostAsJsonAsync("/quotes", new { Text = (string?)null });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await resp.Content
            .ReadFromJsonAsync<JsonElement>(JsonOpts);
        problem.GetProperty("status").GetInt32().Should().Be(400);
    }

    // Test 12 — GET by id after creating
    [Fact]
    public async Task GetQuoteById_AfterCreate_Returns200()
    {
        AuthAs("author-4");

        // Create via HTTP
        var createResp = await _client.PostAsJsonAsync("/quotes", new { Text = "Live long." });
        var created = await createResp.Content.ReadFromJsonAsync<QuoteEntity>(JsonOpts);

        // Fetch by id
        var resp = await _client.GetAsync($"/quotes/{created!.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await resp.Content.ReadFromJsonAsync<QuoteEntity>(JsonOpts);
        fetched!.Text.Should().Be("Live long.");
    }

    // Test 13
    [Fact]
    public async Task GetQuoteById_NonExistent_Returns404()
    {
        AuthAs("user-9");
        var resp = await _client.GetAsync("/quotes/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT: scope-based authorization ────────────────────────────────────────

    // Test 14
    [Fact]
    public async Task PutQuote_WithWriteScope_Returns200()
    {
        AuthAs("editor", scope: "quotes.write");
        var resp = await _client.PutAsJsonAsync("/quotes/1", "Updated text");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Test 15
    [Fact]
    public async Task PutQuote_WithoutWriteScope_Returns403()
    {
        AuthAs("editor-no-scope"); // no scope claim
        var resp = await _client.PutAsJsonAsync("/quotes/1", "Updated text");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Test 16
    [Fact]
    public async Task PutQuote_NoAuth_Returns401()
    {
        var resp = await _client.PutAsJsonAsync("/quotes/1", "Updated text");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE: ownership policy ──────────────────────────────────────────────

    // Test 17
    [Fact]
    public async Task DeleteQuote_AsOwner_Returns200()
    {
        // The policy only checks that route ownerId == token sub
        AuthAs("owner-user");
        var resp = await _client.DeleteAsync("/quotes/1/owner/owner-user");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Test 18
    [Fact]
    public async Task DeleteQuote_AsNonOwner_Returns403()
    {
        AuthAs("attacker"); // sub = "attacker", but ownerId = "victim"
        var resp = await _client.DeleteAsync("/quotes/1/owner/victim");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── WEATHERFORECAST ───────────────────────────────────────────────────────

    // Test 19
    [Fact]
    public async Task GetWeatherForecast_NoAuth_Returns401()
    {
        var resp = await _client.GetAsync("/weatherforecast");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Test 20
    [Fact]
    public async Task GetWeatherForecast_Authenticated_Returns200()
    {
        AuthAs("user-weather");
        var resp = await _client.GetAsync("/weatherforecast");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── EF MIGRATIONS VERIFICATION ────────────────────────────────────────────

    // Test 21
    [Fact]
    public async Task DbMigrations_Applied_QuotesTableExists()
    {
        // Prove migrations ran: insert + query via DbContext
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.Contains("InitialCreate"),
            "InitialCreate migration must be applied");

        // Prove the table is usable
        db.Quotes.Add(new QuoteEntity
        {
            OwnerId   = "verify-user",
            Text      = "Migration verification quote",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var count = await db.Quotes.CountAsync();
        count.Should().Be(1);
    }

    // ── REFRESH TOKEN REUSE DETECTION (end-to-end chain) ─────────────────────

    // Test 22
    [Fact]
    public async Task PostRefresh_ReuseDetection_RevokesEntireFamily()
    {
        // Step A: issue initial pair
        var first = await (await _client.PostAsJsonAsync("/auth/token",
            new { UserId = "chain-user", Scopes = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);

        // Step B: legitimate rotation
        var second = await (await _client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = first!.RefreshToken }))
            .Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);

        // Step C: replay the already-consumed first token → 401
        var reuseResp = await _client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = first.RefreshToken });
        reuseResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "replaying a used refresh token must be rejected");

        // Step D: second token is also revoked (family nuked)
        var nukedResp = await _client.PostAsJsonAsync("/auth/refresh",
            new { RefreshToken = second!.RefreshToken });
        nukedResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "entire token family must be revoked on reuse detection");
    }

    // ── shared DTO ────────────────────────────────────────────────────────────

    private record TokenResponse(string AccessToken, string RefreshToken);
}
