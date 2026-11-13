using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ProyectoFinal.Controllers;

[Authorize]
public sealed class RoleSelectionController(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cambiar(string roleId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = string.IsNullOrWhiteSpace(userId) ? null : await users.FindByIdAsync(userId);
        var role = await roles.FindByIdAsync(roleId);
        if (user is null || role?.Name is null || !await users.IsInRoleAsync(user, role.Name))
            return Forbid();

        HttpContext.Session.SetString(ProyectoFinal.Services.ActiveRoleMiddleware.SessionKey, role.Id);
        TempData["Success"] = $"Rol activo: {role.Name}. Los permisos se actualizaron para esta sesión.";
        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToPage("/Usuario/Panel", new { area = "Identity" });
    }
}
