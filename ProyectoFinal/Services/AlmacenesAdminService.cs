using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class AlmacenesAdminService(ApplicationDbContext db, InventarioService inventory) : IAlmacenesAdminService
{
    public async Task<IReadOnlyList<AlmacenAdminRowViewModel>> ListarAsync()
    {
        var warehouses = await db.Almacenes.AsNoTracking().OrderByDescending(item => item.EsPredeterminado)
            .ThenBy(item => item.Nombre).ToListAsync();
        var rows = new List<AlmacenAdminRowViewModel>(warehouses.Count);
        foreach (var warehouse in warehouses)
        {
            rows.Add(new AlmacenAdminRowViewModel(warehouse.IdAlmacen, warehouse.Nombre, warehouse.Activo,
                warehouse.EsPredeterminado, warehouse.FechaActivacionUtc is not null,
                await db.SaldosAlmacen.AsNoTracking().CountAsync(item => item.IdAlmacen == warehouse.IdAlmacen && item.CantidadBase > 0),
                await TieneTrabajoPendienteAsync(warehouse.IdAlmacen)));
        }
        return rows;
    }

    public Task<Almacen?> ObtenerAsync(int id) => db.Almacenes.AsNoTracking()
        .FirstOrDefaultAsync(item => item.IdAlmacen == id);

    public Task<bool> NombreExisteAsync(string nombre, int? exceptoId) => db.Almacenes
        .AnyAsync(item => item.Nombre == nombre && item.IdAlmacen != exceptoId);

    public async Task<ResultadoAlmacenAdmin> GuardarAsync(AlmacenFormViewModel model, ClaimsPrincipal user)
    {
        var warehouse = model.IdAlmacen is { } id
            ? await db.Almacenes.FirstOrDefaultAsync(item => item.IdAlmacen == id) : null;
        if (model.IdAlmacen is not null && warehouse is null) return new(false, false, "Almacén no encontrado.");
        if (await NombreExisteAsync(model.Nombre, model.IdAlmacen))
            return new(true, false, "Ya existe un almacén con ese nombre.");
        if (warehouse is null)
        {
            warehouse = new Almacen { Nombre = model.Nombre, Activo = true, EsPredeterminado = false };
            db.Almacenes.Add(warehouse);
        }
        else if (!warehouse.EsPredeterminado) warehouse.Nombre = model.Nombre;
        await db.SaveChangesAsync();
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "Almacen", warehouse.IdAlmacen,
            model.IdAlmacen is null ? "Almacén creado" : "Almacén actualizado", null, warehouse.Nombre,
            warehouse.EsPredeterminado ? "Almacén predeterminado protegido." : null));
        await db.SaveChangesAsync();
        var message = warehouse.EsPredeterminado
            ? "El almacén predeterminado se conserva sin sede y no puede renombrarse."
            : model.IdAlmacen is null ? "Almacén creado. Registra un corte inicial antes de vender desde este almacén."
                : "Almacén actualizado.";
        return new(true, true, message);
    }

    public async Task<ResultadoAlmacenAdmin> CambiarEstadoAsync(int id, ClaimsPrincipal user)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        await inventory.BloquearInventarioAsync();
        var warehouse = await db.Almacenes.FirstOrDefaultAsync(item => item.IdAlmacen == id);
        if (warehouse is null) return new(false, false, "Almacén no encontrado.");
        if (warehouse.EsPredeterminado) return new(true, false, "El almacén predeterminado no se puede desactivar.");
        if (warehouse.Activo && (await db.SaldosAlmacen.AnyAsync(item => item.IdAlmacen == id && item.CantidadBase > 0)
            || await TieneTrabajoPendienteAsync(id)))
            return new(true, false, "No se puede desactivar: transfiere o agota el stock y termina los trabajos pendientes.");
        var wasActive = warehouse.Activo;
        warehouse.Activo = !warehouse.Activo;
        await db.SaveChangesAsync();
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "Almacen", id,
            warehouse.Activo ? "Almacén activado" : "Almacén desactivado",
            wasActive ? "Activo" : "Inactivo", warehouse.Activo ? "Activo" : "Inactivo"));
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, true, warehouse.Activo ? "Almacén activado." : "Almacén desactivado.");
    }

    private Task<bool> TieneTrabajoPendienteAsync(int id) => db.Venta.AsNoTracking().AnyAsync(sale =>
        sale.IdAlmacen == id && sale.Estado == EstadoVenta.Confirmada && sale.Pedido != null
        && sale.Pedido.EstadoPedido != EstadoPedido.Entregado && sale.Pedido.EstadoPedido != EstadoPedido.Cancelado);
}
