using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public enum MenuPermissionKind
{
    View,
    Create,
    Edit,
    Delete,
    Process
}

public sealed record MenuNavigationItem(string Codigo, string Nombre, string ControllerNombre, int Orden);

public interface IMenuAccessService
{
    bool RequiresMenuPermission(string controller);
    Task<bool> CanAsync(ClaimsPrincipal user, string controller, string action, string httpMethod);
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string menuCode, MenuPermissionKind permission);
    Task<IReadOnlyList<MenuNavigationItem>> GetNavigationMenusAsync(ClaimsPrincipal user);
}

public sealed class MenuAccessService(ApplicationDbContext context) : IMenuAccessService
{
    private static readonly IReadOnlyDictionary<string, string> MenuCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Clientes"] = "clientes",
        ["Proveedors"] = "proveedores",
        ["Productoes"] = "productos",
        ["Cotizacions"] = "cotizaciones",
        ["PreCotizacion"] = "cotizaciones",
        ["Ventas"] = "ventas",
        ["Taller"] = "taller"
    };

    public bool RequiresMenuPermission(string controller) => MenuCodes.ContainsKey(controller);

    public async Task<bool> CanAsync(ClaimsPrincipal user, string controller, string action, string httpMethod)
    {
        if (!MenuCodes.TryGetValue(controller, out var menuCode)) return true;
        var permission = controller.Equals("PreCotizacion", StringComparison.OrdinalIgnoreCase)
            ? MenuPermissionKind.Create
            : GetActionPermission(action, httpMethod);
        return await HasPermissionAsync(user, menuCode, permission);
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string menuCode, MenuPermissionKind permission)
    {
        if (user.IsInRole("Admin")) return true;
        if (user.Identity?.IsAuthenticated != true) return false;

        var roleNames = user.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roleNames.Length == 0) return false;

        var permissions = context.PermisoRolMenu.AsNoTracking()
            .Where(item => item.Menu != null
                && item.Menu.Codigo == menuCode
                && item.Menu.Activo
                && item.Rol != null
                && item.Rol.Name != null
                && roleNames.Contains(item.Rol.Name));

        return permission switch
        {
            MenuPermissionKind.View => await permissions.AnyAsync(item => item.PuedeVer),
            MenuPermissionKind.Create => await permissions.AnyAsync(item => item.PuedeCrear),
            MenuPermissionKind.Edit => await permissions.AnyAsync(item => item.PuedeEditar),
            MenuPermissionKind.Delete => await permissions.AnyAsync(item => item.PuedeEliminar),
            MenuPermissionKind.Process => await permissions.AnyAsync(item => item.PuedeProcesar),
            _ => false
        };
    }

    public async Task<IReadOnlyList<MenuNavigationItem>> GetNavigationMenusAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin"))
        {
            var catalog = await context.MenuSistema.AsNoTracking()
                .Where(menu => menu.Activo)
                .OrderBy(menu => menu.Orden)
                .Select(menu => new MenuNavigationItem(menu.Codigo, menu.Nombre, menu.ControllerNombre, menu.Orden))
                .ToListAsync();
            return catalog.Count > 0 ? catalog : new[]
            {
                new MenuNavigationItem("clientes", "Clientes", "Clientes", 10),
                new MenuNavigationItem("proveedores", "Proveedores", "Proveedors", 20),
                new MenuNavigationItem("productos", "Productos", "Productoes", 30),
                new MenuNavigationItem("cotizaciones", "Cotizaciones", "Cotizacions", 40),
                new MenuNavigationItem("ventas", "Ventas", "Ventas", 50),
                new MenuNavigationItem("taller", "Taller", "Taller", 60)
            };
        }

        var roleNames = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToArray();
        if (roleNames.Length == 0) return Array.Empty<MenuNavigationItem>();

        return await context.PermisoRolMenu.AsNoTracking()
            .Where(item => item.PuedeVer
                && item.Menu != null && item.Menu.Activo
                && item.Rol != null && item.Rol.Name != null
                && roleNames.Contains(item.Rol.Name))
            .Select(item => new MenuNavigationItem(item.Menu!.Codigo, item.Menu.Nombre, item.Menu.ControllerNombre, item.Menu.Orden))
            .Distinct()
            .OrderBy(menu => menu.Orden)
            .ToListAsync();
    }

    private static MenuPermissionKind GetActionPermission(string action, string httpMethod) => action switch
    {
        "Create" => MenuPermissionKind.Create,
        "Edit" => MenuPermissionKind.Edit,
        "Delete" => MenuPermissionKind.Delete,
        "ConfirmarDesdeCotizacion" or "Cancelar" or "ActualizarEstado" => MenuPermissionKind.Process,
        _ when HttpMethods.IsGet(httpMethod) || HttpMethods.IsHead(httpMethod) => MenuPermissionKind.View,
        _ => MenuPermissionKind.Process
    };
}
