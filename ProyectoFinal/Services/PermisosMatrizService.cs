using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class PermisosMatrizService(ApplicationDbContext db) : IPermisosMatrizService
{
    public async Task<Dictionary<int, PermisoRolMenu>> ObtenerActualesAsync(string roleId) =>
        await db.PermisoRolMenu.AsNoTracking().Where(item => item.RoleId == roleId)
            .ToDictionaryAsync(item => item.MenuSistemaId);

    public async Task GuardarAsync(string roleId, IReadOnlyList<MenuSistema> menus,
        IReadOnlyList<MenuPermisoItemViewModel> submitted)
    {
        var existing = await db.PermisoRolMenu.Where(item => item.RoleId == roleId)
            .ToDictionaryAsync(item => item.MenuSistemaId);
        var byId = submitted.ToDictionary(item => item.MenuId);
        foreach (var menu in menus)
        {
            var item = byId[menu.IdMenu];
            if (!item.PuedeVer && !item.PuedeCrear && !item.PuedeEditar && !item.PuedeEliminar && !item.PuedeProcesar)
            {
                if (existing.TryGetValue(menu.IdMenu, out var removed)) db.PermisoRolMenu.Remove(removed);
                continue;
            }
            if (!existing.TryGetValue(menu.IdMenu, out var permission))
            {
                permission = new PermisoRolMenu { RoleId = roleId, MenuSistemaId = menu.IdMenu };
                db.PermisoRolMenu.Add(permission);
            }
            permission.PuedeVer = item.PuedeVer;
            permission.PuedeCrear = item.PuedeCrear;
            permission.PuedeEditar = item.PuedeEditar;
            permission.PuedeEliminar = item.PuedeEliminar;
            permission.PuedeProcesar = item.PuedeProcesar;
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<MenuSistema>> GetRoutableMenusAsync(bool tracking = false)
    {
        var query = db.MenuSistema.AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        var rows = await query.OrderBy(menu => menu.Orden).ThenBy(menu => menu.IdMenu).ToListAsync();
        var byId = rows.ToDictionary(menu => menu.IdMenu);
        foreach (var menu in rows)
        {
            if (!menu.Activo || !HasActiveAncestors(menu, byId)) continue;
            var cursor = menu;
            var ancestors = new List<string>();
            var seen = new HashSet<int> { menu.IdMenu };
            while (cursor.IdMenuPadre is { } parentId && byId.TryGetValue(parentId, out var parent) && seen.Add(parentId))
            {
                menu.Profundidad++;
                ancestors.Add(parent.Nombre);
                cursor = parent;
            }
            ancestors.Reverse();
            menu.ContextoJerarquia = string.Join(" / ", ancestors);
        }
        return rows.Where(menu => menu.Activo && HasActiveAncestors(menu, byId) && MenuDestinationRegistry.TryGet(
                menu.Destino ?? MenuDestinationRegistry.LegacyDestination(menu.Codigo), out _))
            .ToList();
    }

    private static bool HasActiveAncestors(MenuSistema menu, IReadOnlyDictionary<int, MenuSistema> byId)
    {
        var seen = new HashSet<int> { menu.IdMenu };
        var parentId = menu.IdMenuPadre;
        while (parentId is { } id)
        {
            if (!seen.Add(id) || !byId.TryGetValue(id, out var parent) || !parent.Activo) return false;
            parentId = parent.IdMenuPadre;
        }
        return true;
    }
}
