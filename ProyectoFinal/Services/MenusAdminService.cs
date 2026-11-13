using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class MenusAdminService(ApplicationDbContext db, ITransaccionIdentidadService transactions) : IMenusAdminService
{
    public async Task<List<MenuSistema>> ListarAsync(bool tracking = false)
    {
        var query = db.MenuSistema.AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        return await query.OrderBy(item => item.Orden).ThenBy(item => item.IdMenu).ToListAsync();
    }

    public Task<MenuSistema?> ObtenerAsync(int id, bool tracking = false) => tracking
        ? db.MenuSistema.FirstOrDefaultAsync(item => item.IdMenu == id)
        : db.MenuSistema.AsNoTracking().FirstOrDefaultAsync(item => item.IdMenu == id);

    public Task<bool> CodigoExisteAsync(string codigo, int? exceptoId) => db.MenuSistema
        .AnyAsync(item => item.Codigo == codigo && item.IdMenu != exceptoId);

    public Task<bool> DestinoExisteAsync(string destino, int? exceptoId) => db.MenuSistema
        .AnyAsync(item => item.Destino == destino && item.IdMenu != exceptoId);

    public Task<ITransaccionIdentidad?> IniciarTransaccionAsync() => transactions.IniciarSerializableAsync();

    public async Task<MenuSistema> GuardarAsync(MenuSistema? item, MenuAdminFormViewModel model, ClaimsPrincipal user)
    {
        if (item is null)
        {
            item = new MenuSistema { Codigo = model.Codigo, Activo = true };
            db.MenuSistema.Add(item);
        }
        item.Nombre = model.Nombre;
        item.Icono = model.Icono;
        item.IdMenuPadre = model.IdMenuPadre;
        item.Orden = model.Orden;
        item.Destino = model.Destino;
        item.ControllerNombre = model.Destino is null ? null : MenuDestinationRegistry.ControllerFor(model.Destino);
        await db.SaveChangesAsync();
        var action = model.IdMenu is null ? "Menú creado" : "Menú actualizado";
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "MenuSistema", item.IdMenu, action, null, item.Nombre,
            $"Código {item.Codigo}; destino {item.Destino ?? "agrupador"}; superior {item.IdMenuPadre?.ToString() ?? "raíz"}."));
        await db.SaveChangesAsync();
        return item;
    }

    public async Task CambiarEstadoAsync(MenuSistema item, ClaimsPrincipal user)
    {
        item.Activo = !item.Activo;
        await db.SaveChangesAsync();
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "MenuSistema", item.IdMenu,
            item.Activo ? "Menú activado" : "Menú desactivado", item.Activo ? "Inactivo" : "Activo",
            item.Activo ? "Activo" : "Inactivo"));
        await db.SaveChangesAsync();
    }
}
