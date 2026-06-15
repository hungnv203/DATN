using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace MovieBooking.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Action { get; }

    public HasPermissionAttribute(string action)
    {
        Action = action;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Skip authorization if AllowAnonymous is present
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var controllerName = context.RouteData.Values["controller"]?.ToString();
        var permissionName = $"Permissions.{controllerName}.{Action}";

        var authorizationService = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorizationService.AuthorizeAsync(context.HttpContext.User, permissionName);

        if (!result.Succeeded)
        {
            context.Result = new ForbidResult();
        }
    }
}
