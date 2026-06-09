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
            // Not authenticated at all — fail
            context.Fail();
            return Task.CompletedTask;
        }

        // The resource is the HttpContext; read the ownerId route value from it
        if (context.Resource is HttpContext httpContext)
        {
            var ownerId = httpContext.GetRouteValue("ownerId")?.ToString();

            if (ownerId == userId)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail(); // Different user — deny
            }
        }

        return Task.CompletedTask;
    }
}
