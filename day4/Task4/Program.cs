using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();
});

var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required in configuration");

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "smart";
})
.AddPolicyScheme("smart", "Smart Scheme", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);

            if (jwt.Issuer.Contains("login.microsoftonline.com") ||
                jwt.Issuer.Contains("sts.windows.net"))
                return "Entra";
        }

        return "InternalJwt";
    };
})
.AddJwtBearer("InternalJwt", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer    = "your-app",
        ValidAudience  = "your-audience",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
    };
})
.AddJwtBearer("Entra", options =>
{
    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var clientId = builder.Configuration["AzureAd:ClientId"];

    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidAudience = clientId,
        ValidIssuer   = $"https://sts.windows.net/{tenantId}/"
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", p =>
        p.RequireClaim("scope", "quotes.write"));

    options.AddPolicy("can-delete-own-quote", p =>
        p.AddRequirements(new QuoteOwnerRequirement()));
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthorizationHandler, QuoteOwnerHandler>();
builder.Services.AddControllers();

builder.Services.AddDbContext<QuotesDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=quotes.db"));

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    using (LogContext.PushProperty("TraceId", ctx.TraceIdentifier))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Required so WebApplicationFactory<Program> in the test project can reference this type.
public partial class Program { }
