using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

public class QuoteOwnerHandlerTests
{
    // ─── helpers ─────────────────────────────────────────────────────────────

    private static IAuthorizationService BuildAuthService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(opts =>
            opts.AddPolicy("can-delete-own-quote",
                p => p.AddRequirements(new QuoteOwnerRequirement())));
        services.AddSingleton<IAuthorizationHandler>(new QuoteOwnerHandler());
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal AuthenticatedUser(string userId, string claimType = "sub")
    {
        var identity = new ClaimsIdentity(
            [new Claim(claimType, userId)],
            authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal AnonymousUser() =>
        new ClaimsPrincipal(new ClaimsIdentity());

    private static HttpContext HttpContextWithOwnerId(string ownerId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues = new RouteValueDictionary { ["ownerId"] = ownerId };
        return ctx;
    }

    // ─── Success path ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_UserMatchesOwner_Succeeds()
    {
        // Arrange
        var svc = BuildAuthService();
        var user = AuthenticatedUser("user-123");
        var resource = HttpContextWithOwnerId("user-123");

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_UserIdentifiedViaNameIdentifierFallback_Succeeds()
    {
        // Arrange — token uses NameIdentifier instead of "sub"
        var svc = BuildAuthService();
        var user = AuthenticatedUser("user-999", ClaimTypes.NameIdentifier);
        var resource = HttpContextWithOwnerId("user-999");

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    // ─── Failure: wrong owner ────────────────────────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_UserIsNotOwner_Fails()
    {
        // Arrange — authenticated as user-123 but trying to delete user-456's quote
        var svc = BuildAuthService();
        var user = AuthenticatedUser("user-123");
        var resource = HttpContextWithOwnerId("user-456");

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("user-111", "user-222")]
    [InlineData("alice",    "bob"     )]
    [InlineData("owner-A",  "owner-B" )]
    public async Task HandleRequirementAsync_DifferentUserAndOwner_Fails(string userId, string ownerId)
    {
        // Arrange
        var svc = BuildAuthService();
        var user = AuthenticatedUser(userId);
        var resource = HttpContextWithOwnerId(ownerId);

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    // ─── Failure: unauthenticated ────────────────────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_AnonymousUser_Fails()
    {
        // Arrange
        var svc = BuildAuthService();
        var user = AnonymousUser();
        var resource = HttpContextWithOwnerId("user-123");

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_UserHasNoSubOrNameIdentifierClaim_Fails()
    {
        // Arrange — authenticated but wrong claim type (e.g. only "email")
        var svc = BuildAuthService();
        var identity = new ClaimsIdentity(
            [new Claim("email", "user@example.com")],
            authenticationType: "Test");
        var user = new ClaimsPrincipal(identity);
        var resource = HttpContextWithOwnerId("user@example.com");

        // Act
        var result = await svc.AuthorizeAsync(user, resource, "can-delete-own-quote");

        // Assert
        result.Succeeded.Should().BeFalse();
    }
}
