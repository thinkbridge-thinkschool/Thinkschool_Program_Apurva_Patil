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
/// Starts the Quotes API in-process with a test-friendly JWT configuration:
///   - InternalJwt uses a known test key so tests can mint tokens.
///   - Entra is neutered so tests never reach Azure AD.
///   - ClockSkew = 0 on InternalJwt so that "expired token" tests work immediately.
///   - DbContext is replaced with an in-memory SQLite database.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "integration-test-key-must-be-32chars!!";

    // Keep-alive connection ensures the in-memory SQLite DB persists for the
    // entire test class lifetime (SQLite in-memory dies when last connection closes).
    private readonly SqliteConnection _keepAlive = new("DataSource=:memory:");

    public TestWebAppFactory()
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

            // ── Replace InternalJwt params with the test key and zero clock skew ──
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

            // ── Prevent the Entra scheme from reaching Azure AD ───────────────
            services.PostConfigure<JwtBearerOptions>("Entra", options =>
            {
                options.Authority         = null!;
                options.MetadataAddress   = null!;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = false,
                    ValidateAudience         = false,
                    ValidateIssuerSigningKey  = false,
                    SignatureValidator = (token, _) => new JwtSecurityToken(token)
                };
            });
        });
    }

    /// <summary>Applies EF migrations to the in-memory database.</summary>
    public void ApplyMigrations()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<QuotesDbContext>()
            .Database.Migrate();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
