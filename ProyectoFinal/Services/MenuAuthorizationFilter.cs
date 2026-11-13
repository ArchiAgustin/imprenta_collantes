using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProyectoFinal.Services;

public sealed class MenuAuthorizationFilter(IMenuAccessService menuAccess) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        if (!menuAccess.RequiresMenuPermission(controller, action)) return;

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (!await menuAccess.CanAsync(user, controller, action, context.HttpContext.Request.Method))
            context.Result = new ForbidResult();
    }
}
