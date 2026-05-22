using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Testcontainers.MsSql;
using Xunit;

public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestSigningKey = "integration-test-key-must-be-32chars!!";

    // Spins up a real SQL Server 2022 container
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    // IAsyncLifetime — starts container before any tests run
    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
    }

    // IAsyncLifetime — stops container after all tests finish
    public new async Task DisposeAsync()
    {
        await _sqlContainer.StopAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"]    = TestSigningKey,
                ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000000"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ── Replace SQLite with real SQL Server from container ────────────
            services.RemoveAll<DbContextOptions<QuotesDbContext>>();
            services.RemoveAll<QuotesDbContext>();
            services.AddDbContext<QuotesDbContext>(options =>
                options.UseSqlServer(_sqlContainer.GetConnectionString()));

            // ── Same JWT test config as before ───────────────────────────────
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

            // ── Prevent Entra from reaching Azure AD ─────────────────────────
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
                    SignatureValidator = (token, _) => new JwtSecurityToken(token)
                };
            });
        });
    }

    public void ApplyMigrations()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<QuotesDbContext>()
            .Database.Migrate();
    }
}