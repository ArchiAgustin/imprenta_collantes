using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Models;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public class RolesController : Controller
{
    private static readonly HashSet<string> ProtectedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin"
    };

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<IdentityUser> _userManager;

    public RolesController(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles.OrderBy(role => role.Name).ToListAsync();
        var model = new List<RolAdminListViewModel>(roles.Count);

        foreach (var role in roles)
        {
            var name = role.Name ?? string.Empty;
            var users = string.IsNullOrEmpty(name)
                ? Array.Empty<IdentityUser>()
                : await _userManager.GetUsersInRoleAsync(name);

            model.Add(new RolAdminListViewModel
            {
                Id = role.Id,
                Nombre = name,
                UsuariosAsignados = users.Count,
                EsProtegido = ProtectedRoles.Contains(name)
            });
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? roleName)
    {
        var normalizedName = roleName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 256)
        {
            ModelState.AddModelError(nameof(roleName), "Ingresa un nombre de hasta 256 caracteres.");
            return View();
        }

        if (await _roleManager.RoleExistsAsync(normalizedName))
        {
            ModelState.AddModelError(nameof(roleName), "Ya existe un rol con ese nombre.");
            return View();
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(normalizedName));
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View();
        }

        TempData["Success"] = $"Se creó el rol «{normalizedName}».";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (ProtectedRoles.Contains(role.Name ?? string.Empty))
        {
            TempData["Error"] = "El rol Admin está protegido y no se puede renombrar.";
            return RedirectToAction(nameof(Index));
        }

        return View(new RolAdminEditViewModel { Id = role.Id, Nombre = role.Name ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, RolAdminEditViewModel model)
    {
        if (!string.Equals(id, model.Id, StringComparison.Ordinal)) return NotFound();

        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (ProtectedRoles.Contains(role.Name ?? string.Empty))
        {
            TempData["Error"] = "El rol Admin está protegido y no se puede renombrar.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedName = model.Nombre?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && !string.Equals(role.Name, normalizedName, StringComparison.OrdinalIgnoreCase)
            && await _roleManager.RoleExistsAsync(normalizedName))
        {
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe un rol con ese nombre.");
        }

        model.Id = role.Id;
        model.Nombre = normalizedName ?? string.Empty;
        if (!ModelState.IsValid)
            return View(model);

        role.Name = model.Nombre;
        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        TempData["Success"] = $"Se actualizó el rol a «{role.Name}».";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        if (ProtectedRoles.Contains(role.Name ?? string.Empty))
        {
            TempData["Error"] = "Los roles base Admin y User no se pueden eliminar.";
            return RedirectToAction(nameof(Index));
        }

        var users = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (users.Count > 0)
        {
            TempData["Error"] = "Primero quita este rol de todos los usuarios.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Se eliminó el rol «{role.Name}».";
        return RedirectToAction(nameof(Index));
    }
}
