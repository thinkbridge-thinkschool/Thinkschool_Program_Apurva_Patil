using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

/// <summary>
/// WebApplicationFactory for Quotes.Tests.Integration.
///
/// Every test that creates an IntegrationTestFactory gets:
///   - A unique in-memory SQLite database (isolated from other tests)
///   - EF migrations applied via the open keep-alive connection
///   - IClock replaced with a controllable FakeClock
///   - JWT signed with a known test key; Entra scheme neutered
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "integration-test-key-must-be-32chars!!";

    // Keep-alive connection: SQLite in-memory databases live as long as at
    // least one connection is open.  This field persists the DB for the
    // entire factory (= one test).
    private readonly SqliteConnection _keepAlive = new("DataSource=:memory:");

    public FakeClock Clock { get; } = new(new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc));

    public IntegrationTestFactory()
    {
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"]      = TestSigningKey,
                ["AzureAd:TenantId"]   = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"]   = "00000000-0000-0000-0000-000000000000"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ── Replace DbContext with in-memory SQLite ───────────────────────
            services.RemoveAll<DbContextOptions<QuotesDbContext>>();
            services.RemoveAll<QuotesDbContext>();
            services.AddDbContext<QuotesDbContext>(options =>
                options.UseSqlite(_keepAlive));

            // ── Replace IClock with a deterministic fake ──────────────────────
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            // ── InternalJwt: test signing key + zero clock skew ───────────────
            services.PostConfigure<JwtBearerOptions>("InternalJwt", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer      = "your-app",
                    ValidAudience    = "your-audience",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(TestSigningKey)),
                    ClockSkew        = TimeSpan.Zero
                };
            });

            // ── Entra: disabled (no test token has an Entra issuer) ───────────
            services.PostConfigure<JwtBearerOptions>("Entra", options =>
            {
                options.Authority            = null!;
                options.MetadataAddress      = null!;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer          = false,
                    ValidateAudience        = false,
                    ValidateIssuerSigningKey = false,
                    SignatureValidator      = (token, _) => new JwtSecurityToken(token)
                };
            });
        });
    }

    /// <summary>Applies EF Core migrations to the in-memory database.</summary>
    public void ApplyMigrations()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<QuotesDbContext>()
            .Database.Migrate();
    }

    /// <summary>Seeds a quote directly into the DB, bypassing HTTP.</summary>
    public QuoteEntity SeedQuote(string ownerId, string text)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
        var entity = new QuoteEntity
        {
            OwnerId   = ownerId,
            Text      = text,
            CreatedAt = Clock.UtcNow
        };
        db.Quotes.Add(entity);
        db.SaveChanges();
        return entity;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
