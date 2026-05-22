# Day 3 — Task 2: Authorization Policies

---

## Code Submission

### 1. `Program.cs` — Policy Definitions

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
        ValidIssuer = $"https://sts.windows.net/{tenantId}/"
    };
});

builder.Services.AddAuthorization(options =>
{
    // Policy 1: Claim-based — caller must have scope claim "quotes.write"
    options.AddPolicy("can-edit-quotes", p =>
        p.RequireClaim("scope", "quotes.write"));

    // Policy 2: Custom requirement — user can only delete quotes they own
    options.AddPolicy("can-delete-own-quote", p =>
        p.AddRequirements(new QuoteOwnerRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, QuoteOwnerHandler>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```

---

### 2. `Authorization/QuoteOwnerRequirement.cs` — Custom Requirement + Handler

```csharp
using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Custom requirement: the authenticated user must own the quote being acted on.
/// The quote's owner id is passed via the route value "ownerId".
/// </summary>
public class QuoteOwnerRequirement : IAuthorizationRequirement { }

public class QuoteOwnerHandler : AuthorizationHandler<QuoteOwnerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        QuoteOwnerRequirement requirement)
    {
        // Pull the current user's subject claim
        var userId = context.User.FindFirst("sub")?.Value
                  ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        // The resource is the HttpContext; read the ownerId route value from it
        if (context.Resource is HttpContext httpContext)
        {
            var ownerId = httpContext.GetRouteValue("ownerId")?.ToString();

            if (ownerId == userId)
                context.Succeed(requirement);
            else
                context.Fail(); // Different user — deny
        }

        return Task.CompletedTask;
    }
}
```

---

### 3. `controllers/QuotesController.cs` — Policy-Protected Endpoints

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("[controller]")]
public class QuotesController : ControllerBase
{
    // GET /quotes — any authenticated user
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new[]
        {
            new { Id = 1, OwnerId = "user-123", Text = "To be or not to be." },
            new { Id = 2, OwnerId = "user-456", Text = "All that glitters is not gold." }
        });
    }

    // PUT /quotes/{id} — requires scope claim "quotes.write"
    [HttpPut("{id:int}")]
    [Authorize(Policy = "can-edit-quotes")]
    public IActionResult Edit(int id, [FromBody] string text)
    {
        return Ok(new { Id = id, Text = text, Updated = true });
    }

    // DELETE /quotes/{id}/owner/{ownerId}
    // ownerId in the route lets QuoteOwnerHandler compare it against the sub claim.
    [HttpDelete("{id:int}/owner/{ownerId}")]
    [Authorize(Policy = "can-delete-own-quote")]
    public IActionResult Delete(int id, string ownerId)
    {
        return Ok(new { Id = id, Deleted = true });
    }
}
```

---

### 4. `Tests/AuthorizationPolicyTests.cs` — Tests Proving 403 Behaviour

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

public class AuthorizationPolicyTests
{
    // ─── helpers ──────────────────────────────────────────────────────────────

    private static IAuthorizationService BuildAuthService(
        Action<AuthorizationOptions> configure,
        params IAuthorizationHandler[] handlers)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(configure);
        foreach (var h in handlers)
            services.AddSingleton<IAuthorizationHandler>(h);
        return services.BuildServiceProvider()
                       .GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal UserWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static ClaimsPrincipal Anonymous() =>
        new(new ClaimsIdentity()); // no AuthenticationType = unauthenticated

    private static HttpContext MakeHttpContext(string ownerId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues = new RouteValueDictionary { ["ownerId"] = ownerId };
        return ctx;
    }

    // ─── Policy 1: can-edit-quotes ────────────────────────────────────────────

    [Fact]
    public async Task CanEditQuotes_Fails_WhenScopeClaimMissing()
    {
        var svc = BuildAuthService(opts =>
            opts.AddPolicy("can-edit-quotes", p =>
                p.RequireClaim("scope", "quotes.write")));

        var user = UserWith(new Claim("sub", "user-123")); // no scope claim

        var result = await svc.AuthorizeAsync(user, null, "can-edit-quotes");

        Assert.False(result.Succeeded); // → 403
    }

    [Fact]
    public async Task CanEditQuotes_Fails_WhenScopeClaimHasWrongValue()
    {
        var svc = BuildAuthService(opts =>
            opts.AddPolicy("can-edit-quotes", p =>
                p.RequireClaim("scope", "quotes.write")));

        var user = UserWith(
            new Claim("sub", "user-123"),
            new Claim("scope", "quotes.read")); // wrong value

        var result = await svc.AuthorizeAsync(user, null, "can-edit-quotes");

        Assert.False(result.Succeeded); // → 403
    }

    [Fact]
    public async Task CanEditQuotes_Succeeds_WhenScopeClaimMatches()
    {
        var svc = BuildAuthService(opts =>
            opts.AddPolicy("can-edit-quotes", p =>
                p.RequireClaim("scope", "quotes.write")));

        var user = UserWith(
            new Claim("sub", "user-123"),
            new Claim("scope", "quotes.write"));

        var result = await svc.AuthorizeAsync(user, null, "can-edit-quotes");

        Assert.True(result.Succeeded); // → 200
    }

    // ─── Policy 2: can-delete-own-quote ───────────────────────────────────────

    [Fact]
    public async Task CanDeleteOwnQuote_Fails_WhenUserIsNotOwner()
    {
        var svc = BuildAuthService(
            opts => opts.AddPolicy("can-delete-own-quote",
                        p => p.AddRequirements(new QuoteOwnerRequirement())),
            new QuoteOwnerHandler());

        var user = UserWith(new Claim("sub", "user-123"));
        var resource = MakeHttpContext(ownerId: "user-456"); // someone else's quote

        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        Assert.False(result.Succeeded); // → 403
    }

    [Fact]
    public async Task CanDeleteOwnQuote_Fails_WhenUserIsUnauthenticated()
    {
        var svc = BuildAuthService(
            opts => opts.AddPolicy("can-delete-own-quote",
                        p => p.AddRequirements(new QuoteOwnerRequirement())),
            new QuoteOwnerHandler());

        var resource = MakeHttpContext(ownerId: "user-123");

        var result = await svc.AuthorizeAsync(Anonymous(), resource, "can-delete-own-quote");

        Assert.False(result.Succeeded); // → 403
    }

    [Fact]
    public async Task CanDeleteOwnQuote_Succeeds_WhenUserIsOwner()
    {
        var svc = BuildAuthService(
            opts => opts.AddPolicy("can-delete-own-quote",
                        p => p.AddRequirements(new QuoteOwnerRequirement())),
            new QuoteOwnerHandler());

        var user = UserWith(new Claim("sub", "user-123"));
        var resource = MakeHttpContext(ownerId: "user-123"); // same user

        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        Assert.True(result.Succeeded); // → 200
    }
}
```

---

## Reflections

### What did you learn this session?

**Authentication answers "who." Authorization answers "can they."**  
The biggest click was that `[Authorize]` on a controller only checks identity — it says nothing about *what* that identity may do. The moment you need a rule ("must have this scope", "must own this resource"), you need a policy. Attaching rules directly to roles (`RequireRole("admin")`) bleeds business logic into the auth layer; policies keep that logic explicit, named, and testable in isolation without an HTTP stack.

The second insight was the `IAuthorizationRequirement` / `AuthorizationHandler<T>` split: the requirement is a plain data bag, the handler contains the logic and can have services injected (repositories, feature flags, etc.). That separation makes the handler trivially unit-testable — you just call `HandleRequirementAsync` directly or go through `IAuthorizationService` in a DI container you build in three lines.

### What would break this?

| Scenario | Why it breaks |
|---|---|
| **Missing `AddScoped<IAuthorizationHandler, QuoteOwnerHandler>()`** | ASP.NET resolves handlers from DI; if the registration is absent the handler never runs and the requirement is never satisfied → all delete calls return 403, even legitimate owners. |
| **`context.Resource` is not `HttpContext`** | If `AuthorizeAsync` is called with a different resource (e.g. a domain object, or `null`), `GetRouteValue` is unreachable. Handler silently skips `Succeed` → implicit 403. Fix: also handle the case where resource is `null` or check the type defensively. |
| **`sub` claim absent in token** | OAuth2 tokens issued by some IdPs use `oid` instead of `sub`. The fallback to `NameIdentifier` helps, but a token with neither claim causes the handler to call `context.Fail()` → 403 even for the real owner. |
| **`scope` claim sent as space-delimited string** | Some IdPs pack multiple scopes in one claim value: `"quotes.read quotes.write"`. `RequireClaim("scope", "quotes.write")` does an *exact* value match, so `"quotes.read quotes.write"` would fail. Fix: use a custom requirement that calls `HasClaim(c => c.Type == "scope" && c.Value.Split(' ').Contains("quotes.write"))`. |
| **Calling `context.Fail()` in the handler explicitly** | Once `Fail()` is called, no other handler can override it with `Succeed()`. If you later add a second handler for the same requirement, it can never grant access — the explicit failure wins. |
