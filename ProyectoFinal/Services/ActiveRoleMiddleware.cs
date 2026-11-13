using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal.Services;

public sealed class ActiveRoleMiddleware(RequestDelegate next)
{
    public const string SessionKey = "active-role-id";
    public const string AvailableRoleIdClaim = "app:available-role-id";
    public const string AvailableRoleNameClaim = "app:available-role-name";
    public const string ActiveRoleIdClaim = "app:active-role-id";

    public async Task InvokeAsync(HttpContext context, UserManager<IdentityUser> users, RoleManager<IdentityRole> roles)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = string.IsNullOrWhiteSpace(userId) ? null : await users.FindByIdAsync(userId);
        var cookieStamp = context.User.FindFirstValue(new IdentityOptions().ClaimsIdentity.SecurityStampClaimType);
        if (user is null || user.LockoutEnd is { } lockout && lockout > DateTimeOffset.UtcNow.AddYears(50)
            || user.SecurityStamp is { } currentStamp && cookieStamp is { } savedStamp && currentStamp != savedStamp)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            await next(context);
            return;
        }

        var roleNames = await users.GetRolesAsync(user);
        var roleRows = await roles.Roles.AsNoTracking()
            .Where(role => role.Name != null && roleNames.Contains(role.Name))
            .OrderBy(role => role.Name).ThenBy(role => role.Id)
            .ToListAsync();
        var selectedId = context.Session.GetString(SessionKey);
        var selected = roleRows.FirstOrDefault(role => role.Id == selectedId) ?? roleRows.FirstOrDefault();
        if (selected is null) context.Session.Remove(SessionKey);
        else context.Session.SetString(SessionKey, selected.Id);

        var identities = context.User.Identities.Select(identity => new ClaimsIdentity(identity)).ToList();
        var primary = identities.FirstOrDefault(identity => identity.IsAuthenticated) ?? identities.First();
        foreach (var identity in identities)
        {
            foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList()) identity.RemoveClaim(claim);
            foreach (var claim in identity.FindAll(AvailableRoleIdClaim).ToList()) identity.RemoveClaim(claim);
            foreach (var claim in identity.FindAll(AvailableRoleNameClaim).ToList()) identity.RemoveClaim(claim);
            foreach (var claim in identity.FindAll(ActiveRoleIdClaim).ToList()) identity.RemoveClaim(claim);
        }

        foreach (var role in roleRows)
        {
            primary.AddClaim(new Claim(AvailableRoleIdClaim, role.Id));
            primary.AddClaim(new Claim(AvailableRoleNameClaim, role.Name ?? string.Empty));
        }
        if (selected is not null)
        {
            primary.AddClaim(new Claim(primary.RoleClaimType, selected.Name ?? string.Empty));
            primary.AddClaim(new Claim(ActiveRoleIdClaim, selected.Id));
        }

        context.User = new ClaimsPrincipal(identities);
        await next(context);
    }
}
