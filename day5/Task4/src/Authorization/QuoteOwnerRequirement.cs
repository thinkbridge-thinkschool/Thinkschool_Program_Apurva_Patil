using Microsoft.AspNetCore.Authorization;

public class QuoteOwnerRequirement : IAuthorizationRequirement { }

public class QuoteOwnerHandler : AuthorizationHandler<QuoteOwnerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        QuoteOwnerRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value
                  ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        if (context.Resource is HttpContext httpContext)
        {
            var ownerId = httpContext.GetRouteValue("ownerId")?.ToString();

            if (ownerId == userId)
                context.Succeed(requirement);
            else
                context.Fail();
        }

        return Task.CompletedTask;
    }
}
