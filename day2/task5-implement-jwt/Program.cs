using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesJwt.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// DB (in-memory for exercise)
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("quotes"));

// Authentication setup using key from configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var keyBase64 = jwtSection.GetValue<string>("Key") ?? throw new InvalidOperationException("Jwt:Key missing");
var keyBytes = Convert.FromBase64String(keyBase64);
var signingKey = new SymmetricSecurityKey(keyBytes);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidateAudience = true,
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Seed a test user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Users.Any())
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!");
        db.Users.Add(new Models.User { Email = "alice@example.com", PasswordHash = hash });
        db.SaveChanges();
    }
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
