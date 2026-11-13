using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public class PermisosController(IPermisosMatrizService matriz, RoleManager<IdentityRole> roleManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var roles = await roleManager.Roles.OrderBy(role => role.Name).ToListAsync();
        return View(roles);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "El rol Admin conserva acceso total y no se puede restringir desde esta matriz.";
            return RedirectToAction(nameof(Index));
        }

        var menus = await matriz.GetRoutableMenusAsync();
        var current = await matriz.ObtenerActualesAsync(id);
        return View(BuildViewModel(role, menus, current));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(string id, PermisosRolViewModel model)
    {
        if (!string.Equals(id, model.RolId, StringComparison.Ordinal)) return NotFound();
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "El rol Admin conserva acceso total y no se puede restringir desde esta matriz.";
            return RedirectToAction(nameof(Index));
        }

        var menus = await matriz.GetRoutableMenusAsync(tracking: true);
        model.RolNombre = role.Name ?? string.Empty;
        model.Menus ??= new List<MenuPermisoItemViewModel>();
        var submittedMenuIds = model.Menus.Select(menu => menu.MenuId).ToHashSet();
        var activeMenuIds = menus.Select(menu => menu.IdMenu).ToHashSet();
        if (submittedMenuIds.Count != model.Menus.Count || !submittedMenuIds.SetEquals(activeMenuIds))
            ModelState.AddModelError(string.Empty, "La matriz recibida no coincide con los menús activos.");

        if (!ModelState.IsValid)
        {
            var submitted = model.Menus.GroupBy(menu => menu.MenuId).ToDictionary(group => group.Key, group => group.First());
            var viewModel = BuildViewModel(role, menus, new Dictionary<int, PermisoRolMenu>());
            foreach (var item in viewModel.Menus)
            {
                if (!submitted.TryGetValue(item.MenuId, out var value)) continue;
                item.PuedeVer = value.PuedeVer;
                item.PuedeCrear = value.PuedeCrear;
                item.PuedeEditar = value.PuedeEditar;
                item.PuedeEliminar = value.PuedeEliminar;
                item.PuedeProcesar = value.PuedeProcesar;
            }
            return View(viewModel);
        }

        await matriz.GuardarAsync(id, menus, model.Menus);
        TempData["Success"] = $"Se actualizaron los permisos del rol «{role.Name}».";
        return RedirectToAction(nameof(Index));
    }

    private static PermisosRolViewModel BuildViewModel(
        IdentityRole role,
        IEnumerable<MenuSistema> menus,
        IReadOnlyDictionary<int, PermisoRolMenu> permissions) => new()
    {
        RolId = role.Id,
        RolNombre = role.Name ?? string.Empty,
        Menus = menus.Select(menu =>
        {
            permissions.TryGetValue(menu.IdMenu, out var permission);
            var destinationCode = menu.Destino ?? MenuDestinationRegistry.LegacyDestination(menu.Codigo);
            MenuDestinationRegistry.TryGet(destinationCode, out var destination);
            return new MenuPermisoItemViewModel
            {
                MenuId = menu.IdMenu,
                Codigo = menu.Codigo,
                Nombre = menu.Nombre,
                ControllerNombre = menu.ControllerNombre,
                Profundidad = menu.Profundidad,
                DestinoNombre = destination?.Nombre,
                ContextoJerarquia = menu.ContextoJerarquia,
                PuedeVer = permission?.PuedeVer ?? false,
                PuedeCrear = permission?.PuedeCrear ?? false,
                PuedeEditar = permission?.PuedeEditar ?? false,
                PuedeEliminar = permission?.PuedeEliminar ?? false,
                PuedeProcesar = permission?.PuedeProcesar ?? false
            };
        }).ToList()
    };

}
