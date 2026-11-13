using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class InventarioConsultaService(ApplicationDbContext db) : IInventarioConsultaService
{
    public async Task<InventarioIndexViewModel> ObtenerInicioAsync()
    {
        var warehouses = await db.Almacenes.AsNoTracking().Where(item => item.Activo)
            .OrderByDescending(item => item.EsPredeterminado).ThenBy(item => item.Nombre).ToListAsync();
        var serie = await db.SeriesInventario.AsNoTracking().SingleOrDefaultAsync(item => item.Activa);
        var inventarios = await db.Inventarios.AsNoTracking().Include(item => item.Almacen)
            .OrderByDescending(item => item.IdInventario).Take(200)
            .Select(item => new InventarioTablaFilaViewModel(item.IdInventario, item.Serie, item.Correlativo,
                item.Fecha, item.Almacen.Nombre, item.Detalles.Count)).ToListAsync();
        var total = await db.Producto.AsNoTracking().CountAsync(item => !item.EsServicio);
        var withoutUnit = await db.Producto.AsNoTracking()
            .CountAsync(item => !item.EsServicio && (item.IdUnidadMedidaBase == null
                || !db.UnidadesMedida.Any(unit => unit.IdUnidadMedida == item.IdUnidadMedidaBase && unit.Activo)));
        return new InventarioIndexViewModel
        {
            Inventarios = inventarios, Almacenes = warehouses, TotalArticulos = total,
            ArticulosSinUnidadBase = withoutUnit, Serie = serie?.Serie ?? "INV1",
            SiguienteCorrelativo = serie?.SiguienteCorrelativo ?? 2,
            FechaHoy = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).DateTime)
        };
    }

    public Task<bool> AlmacenActivoAsync(int id) => db.Almacenes.AsNoTracking()
        .AnyAsync(item => item.IdAlmacen == id && item.Activo);

    public async Task<IReadOnlyList<ProductoBusquedaInventario>> BuscarProductosAsync(string termino, int almacenId)
    {
        var products = await db.Producto.AsNoTracking()
            .Where(item => !item.EsServicio && item.UnidadMedidaBase != null && item.UnidadMedidaBase.Activo
                && item.Nombre != null && item.Nombre.Contains(termino))
            .OrderBy(item => item.Nombre).Take(15)
            .Select(item => new { item.IdProducto, item.Nombre,
                UnidadBase = item.UnidadMedidaBase!.DescripcionCorta ?? item.UnidadMedidaBase.Descripcion })
            .ToListAsync();
        var ids = products.Select(item => item.IdProducto).ToArray();
        var balances = await db.SaldosAlmacen.AsNoTracking()
            .Where(item => item.IdAlmacen == almacenId && ids.Contains(item.IdProducto))
            .ToDictionaryAsync(item => item.IdProducto, item => item.CantidadBase);
        return products.Select(item => new ProductoBusquedaInventario(item.IdProducto, item.Nombre,
            item.UnidadBase, balances.TryGetValue(item.IdProducto, out var amount) ? amount : 0m)).ToList();
    }

    public Task<Inventario?> ObtenerDetalleAsync(int id) => db.Inventarios.AsNoTracking()
        .Include(item => item.Almacen).Include(item => item.Detalles)
        .ThenInclude(item => item.Producto).ThenInclude(item => item.UnidadMedidaBase)
        .FirstOrDefaultAsync(item => item.IdInventario == id);

    public async Task<KardexInventarioViewModel?> ObtenerKardexAsync(int? almacenId)
    {
        var warehouses = await db.Almacenes.AsNoTracking().Where(item => item.Activo)
            .OrderByDescending(item => item.EsPredeterminado).ThenBy(item => item.Nombre).ToListAsync();
        if (almacenId is { } requested && !warehouses.Any(item => item.IdAlmacen == requested)) return null;
        IQueryable<Kardex> query = db.Kardex.AsNoTracking().Include(item => item.Producto)
            .ThenInclude(item => item.UnidadMedidaBase).Include(item => item.Almacen);
        if (almacenId is { } selected) query = query.Where(item => item.IdAlmacen == selected);
        return new KardexInventarioViewModel
        {
            AlmacenSeleccionadoId = almacenId, Almacenes = warehouses,
            Movimientos = await query.OrderByDescending(item => item.IdKardex).Take(100).ToListAsync()
        };
    }
}
