using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ✅ Add this block — sets up both auth schemes
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "smart";
})
.AddPolicyScheme("smart", "Smart Scheme", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"]
                                .FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            var jwt = new System.IdentityModel.Tokens.Jwt
                             .JwtSecurityToken(token);

            if (jwt.Issuer.Contains("login.microsoftonline.com") || 
    jwt.Issuer.Contains("sts.windows.net"))
    return "Entra";
        }

        return "InternalJwt";
    };
})
.AddJwtBearer("InternalJwt", options =>
{
    // Your own internal JWT — keep this as it was
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = "your-app",
        ValidAudience = "your-audience",
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("YOUR_SECRET_KEY_MIN_32_CHARS_LONG"))
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
        ValidIssuer = $"https://sts.windows.net/{tenantId}/" // ✅ Add this
    };
});
builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// ✅ These two lines must be in this exact order
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();