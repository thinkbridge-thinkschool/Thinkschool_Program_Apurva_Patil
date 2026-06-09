using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

/// <summary>
/// Unit tests that verify the two authorization policies produce 403-equivalent
/// failures when the caller does not satisfy the policy.
/// 
/// We test the authorization layer directly — no HTTP layer needed — so these
/// run without a running server and without a real JWT.
/// </summary>
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

    private static ClaimsPrincipal UserWith(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal Anonymous()
    {
        // Unauthenticated principal — no AuthenticationType
        var identity = new ClaimsIdentity();
        return new ClaimsPrincipal(identity);
    }

    // ─── Policy 1: can-edit-quotes (claim-based) ──────────────────────────────

    [Fact]
    public async Task CanEditQuotes_Fails_WhenScopeClaimMissing()
    {
        // Arrange — user authenticated but no scope claim at all
        var svc = BuildAuthService(opts =>
            opts.AddPolicy("can-edit-quotes", p =>
                p.RequireClaim("scope", "quotes.write")));

        var user = UserWith(new Claim("sub", "user-123")); // no scope claim

        // Act
        var result = await svc.AuthorizeAsync(user, resource: null, "can-edit-quotes");

        // Assert — should be denied (equivalent to 403)
        Assert.False(result.Succeeded, "Expected authorization failure when scope claim is absent.");
    }

    [Fact]
    public async Task CanEditQuotes_Fails_WhenScopeClaimHasWrongValue()
    {
        // Arrange — user has a scope claim but with read-only value
        var svc = BuildAuthService(opts =>
            opts.AddPolicy("can-edit-quotes", p =>
                p.RequireClaim("scope", "quotes.write")));

        var user = UserWith(
            new Claim("sub", "user-123"),
            new Claim("scope", "quotes.read")); // wrong value

        // Act
        var result = await svc.AuthorizeAsync(user, resource: null, "can-edit-quotes");

        // Assert
        Assert.False(result.Succeeded, "Expected 403 when scope is 'quotes.read', not 'quotes.write'.");
    }

    [Fact]
    public async Task CanEditQuotes_Succeeds_WhenScopeClaimMatches()
    {
        // Arrange
        var svc = BuildAuthService(opts =>
            opts.AddPolicy("can-edit-quotes", p =>
                p.RequireClaim("scope", "quotes.write")));

        var user = UserWith(
            new Claim("sub", "user-123"),
            new Claim("scope", "quotes.write")); // correct

        // Act
        var result = await svc.AuthorizeAsync(user, resource: null, "can-edit-quotes");

        // Assert
        Assert.True(result.Succeeded);
    }

    // ─── Policy 2: can-delete-own-quote (custom requirement) ─────────────────

    private static HttpContext MakeHttpContext(string ownerId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues = new RouteValueDictionary { ["ownerId"] = ownerId };
        return ctx;
    }

    [Fact]
    public async Task CanDeleteOwnQuote_Fails_WhenUserIsNotOwner()
    {
        // Arrange — logged-in as user-123, but trying to delete quote owned by user-456
        var handler = new QuoteOwnerHandler();
        var svc = BuildAuthService(
            opts => opts.AddPolicy("can-delete-own-quote",
                        p => p.AddRequirements(new QuoteOwnerRequirement())),
            handler);

        var user = UserWith(new Claim("sub", "user-123"));
        var resource = MakeHttpContext(ownerId: "user-456"); // different owner

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert — 403: you don't own this quote
        Assert.False(result.Succeeded, "Expected 403 when sub != ownerId.");
    }

    [Fact]
    public async Task CanDeleteOwnQuote_Fails_WhenUserIsUnauthenticated()
    {
        // Arrange — anonymous caller
        var handler = new QuoteOwnerHandler();
        var svc = BuildAuthService(
            opts => opts.AddPolicy("can-delete-own-quote",
                        p => p.AddRequirements(new QuoteOwnerRequirement())),
            handler);

        var resource = MakeHttpContext(ownerId: "user-123");

        // Act
        var result = await svc.AuthorizeAsync(Anonymous(), resource, "can-delete-own-quote");

        // Assert
        Assert.False(result.Succeeded, "Expected 403 for unauthenticated caller.");
    }

    [Fact]
    public async Task CanDeleteOwnQuote_Succeeds_WhenUserIsOwner()
    {
        // Arrange — logged-in as user-123 deleting their own quote
        var handler = new QuoteOwnerHandler();
        var svc = BuildAuthService(
            opts => opts.AddPolicy("can-delete-own-quote",
                        p => p.AddRequirements(new QuoteOwnerRequirement())),
            handler);

        var user = UserWith(new Claim("sub", "user-123"));
        var resource = MakeHttpContext(ownerId: "user-123"); // same owner

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert
        Assert.True(result.Succeeded);
    }
}
