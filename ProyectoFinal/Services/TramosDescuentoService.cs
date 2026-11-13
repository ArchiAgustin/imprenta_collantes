using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class TramosDescuentoService(ApplicationDbContext db) : ITramosDescuentoService
{
    public Task<Producto?> ObtenerProductoAsync(int productoId) => db.Producto.AsNoTracking()
        .FirstOrDefaultAsync(item => item.IdProducto == productoId);

    public async Task<IReadOnlyList<TramoDescuentoProducto>> ListarAsync(int productoId) =>
        await db.TramosDescuentoProducto.AsNoTracking().Where(item => item.IdProducto == productoId)
            .OrderBy(item => item.CantidadMinima).ToListAsync();

    public async Task<bool> AgregarAsync(int productoId, int cantidadMinima, decimal descuentoPorcentaje)
    {
        if (cantidadMinima is < 1 or > 1_000_000 || descuentoPorcentaje is < 0m or > 99.99m
            || decimal.Round(descuentoPorcentaje, 2) != descuentoPorcentaje
            || !await db.Producto.AnyAsync(item => item.IdProducto == productoId)
            || await db.TramosDescuentoProducto.AnyAsync(item => item.IdProducto == productoId && item.CantidadMinima == cantidadMinima))
            return false;
        db.TramosDescuentoProducto.Add(new TramoDescuentoProducto
        {
            IdProducto = productoId, CantidadMinima = cantidadMinima, DescuentoPorcentaje = descuentoPorcentaje
        });
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
    }

    public async Task<bool> EliminarAsync(int productoId, int tramoId)
    {
        var tramo = await db.TramosDescuentoProducto
            .FirstOrDefaultAsync(item => item.Id == tramoId && item.IdProducto == productoId);
        if (tramo is null) return false;
        db.TramosDescuentoProducto.Remove(tramo);
        await db.SaveChangesAsync();
        return true;
    }
}
