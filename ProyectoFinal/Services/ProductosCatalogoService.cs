using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class ProductosCatalogoService(ApplicationDbContext db, InventarioService inventario) : IProductosCatalogoService
{
    private static readonly ResultadoProducto Missing = new(ResultadoProductoEstado.NoEncontrado);
    private static ResultadoProducto Invalid(string key, string message) =>
        new(ResultadoProductoEstado.Invalido, Errores: new Dictionary<string, string> { [key] = message });

    private async Task<List<Almacen>> AlmacenesActivosAsync() => await db.Almacenes.AsNoTracking()
        .Where(item => item.Activo).OrderByDescending(item => item.EsPredeterminado)
        .ThenBy(item => item.Nombre).ToListAsync();

    private static Almacen? SeleccionarAlmacen(IReadOnlyList<Almacen> almacenes, int? id) => id is { } selected
        ? almacenes.FirstOrDefault(item => item.IdAlmacen == selected)
        : almacenes.FirstOrDefault(item => item.EsPredeterminado || item.Nombre == InventarioService.NombreAlmacen);

    public async Task<ListadoProductos?> ListarAsync(int? almacenId)
    {
        var almacenes = await AlmacenesActivosAsync();
        var almacen = SeleccionarAlmacen(almacenes, almacenId);
        if (almacenId is not null && almacen is null) return null;
        var saldos = almacen?.FechaActivacionUtc is null
            ? new Dictionary<int, decimal>()
            : await db.SaldosAlmacen.AsNoTracking().Where(item => item.IdAlmacen == almacen.IdAlmacen)
                .ToDictionaryAsync(item => item.IdProducto, item => item.CantidadBase);
        var productos = await db.Producto.AsNoTracking().Include(p => p.IdProveedorNavigation)
            .Include(p => p.UnidadMedidaBase).ToListAsync();
        return new(productos, almacenes, almacen, saldos);
    }

    public async Task<DetalleProducto?> ObtenerDetalleAsync(int id, int? almacenId)
    {
        var producto = await db.Producto.AsNoTracking().Include(p => p.IdProveedorNavigation)
            .Include(p => p.UnidadMedidaBase).FirstOrDefaultAsync(p => p.IdProducto == id);
        if (producto is null) return null;
        var almacenes = await AlmacenesActivosAsync();
        var almacen = SeleccionarAlmacen(almacenes, almacenId);
        if (almacenId is not null && almacen is null) return null;
        var balance = almacen?.FechaActivacionUtc is not null ? await db.SaldosAlmacen.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdAlmacen == almacen.IdAlmacen && item.IdProducto == id) : null;
        var stock = producto.EsServicio ? "No aplica" : almacen is null
            ? (producto.Stock ?? 0).ToString()
            : almacen.FechaActivacionUtc is null ? "Corte inicial pendiente"
            : balance is null ? "Sin saldo registrado" : balance.CantidadBase.ToString("0.###");
        var presentaciones = await db.PresentacionesProducto.AsNoTracking().Include(item => item.UnidadMedida)
            .Where(item => item.IdProducto == id).OrderByDescending(item => item.Activo)
            .ThenBy(item => item.Descripcion).ToListAsync();
        var unidades = await db.UnidadesMedida.AsNoTracking().Where(item => item.Activo)
            .OrderBy(item => item.Descripcion).ToListAsync();
        return new(producto, almacenes, almacen, stock, presentaciones, unidades);
    }

    public async Task<StockPorAlmacenViewModel?> ObtenerStockPorAlmacenAsync(int id)
    {
        var product = await db.Producto.AsNoTracking().Include(item => item.UnidadMedidaBase)
            .FirstOrDefaultAsync(item => item.IdProducto == id);
        if (product is null) return null;
        var warehouses = await db.Almacenes.AsNoTracking().OrderBy(item => item.Nombre).ToListAsync();
        var balances = product.EsServicio ? new Dictionary<int, decimal>()
            : await db.SaldosAlmacen.AsNoTracking().Where(item => item.IdProducto == id)
                .ToDictionaryAsync(item => item.IdAlmacen, item => item.CantidadBase);
        return new StockPorAlmacenViewModel
        {
            ProductoNombre = product.Nombre ?? "Artículo sin nombre",
            UnidadBase = product.UnidadMedidaBase?.Descripcion,
            EsServicio = product.EsServicio,
            Almacenes = warehouses.Select(warehouse => new StockPorAlmacenFilaViewModel(
                warehouse.Nombre, warehouse.Activo, warehouse.FechaActivacionUtc is not null,
                balances.TryGetValue(warehouse.IdAlmacen, out var amount) ? amount : null)).ToList()
        };
    }

    public Task<Producto?> ObtenerAsync(int id) => db.Producto.AsNoTracking()
        .FirstOrDefaultAsync(item => item.IdProducto == id);

    public Task<Producto?> ObtenerParaEliminarAsync(int id) => db.Producto.AsNoTracking()
        .Include(p => p.IdProveedorNavigation).FirstOrDefaultAsync(p => p.IdProducto == id);

    public async Task<(IReadOnlyList<Proveedor>, IReadOnlyList<UnidadMedida>)> ObtenerOpcionesAsync()
    {
        var proveedores = await db.Proveedor.AsNoTracking().OrderBy(item => item.Nombre).ToListAsync();
        var unidades = await db.UnidadesMedida.AsNoTracking().Where(item => item.Activo)
            .OrderBy(item => item.Descripcion).ToListAsync();
        return (proveedores, unidades);
    }

    public async Task<ResultadoProducto> CrearAsync(Producto producto)
    {
        producto.Stock = 0;
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await inventario.BloquearInventarioAsync();
        var warehouses = await db.Almacenes.AsNoTracking()
            .Where(item => item.Activo && item.FechaActivacionUtc != null).ToListAsync();
        var unit = producto.EsServicio || producto.IdUnidadMedidaBase is null ? null
            : await db.UnidadesMedida.AsNoTracking().FirstOrDefaultAsync(item =>
                item.IdUnidadMedida == producto.IdUnidadMedidaBase && item.Activo);
        if (!producto.EsServicio && unit is null)
            return Invalid(nameof(producto.IdUnidadMedidaBase), "Selecciona una unidad base activa y revisada para el artículo.");
        if (producto.EsServicio) producto.IdUnidadMedidaBase = null;
        if (!producto.EsServicio && unit is not null)
            db.PresentacionesProducto.Add(new PresentacionProducto
            {
                Producto = producto, IdUnidadMedida = unit.IdUnidadMedida,
                Descripcion = unit.DescripcionCorta ?? unit.Descripcion, Factor = 1m
            });
        db.Producto.Add(producto);
        await db.SaveChangesAsync();
        if (!producto.EsServicio && warehouses.Count > 0)
        {
            db.SaldosAlmacen.AddRange(warehouses.Select(warehouse => new StockAlmacen
            {
                IdAlmacen = warehouse.IdAlmacen, IdProducto = producto.IdProducto, CantidadBase = 0m
            }));
            await db.SaveChangesAsync();
        }
        if (transaction is not null) await transaction.CommitAsync();
        return new(ResultadoProductoEstado.Exito, producto);
    }

    public async Task<ResultadoProducto> AgregarPresentacionAsync(int id, PresentacionProductoFormViewModel model, ClaimsPrincipal user)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await inventario.BloquearInventarioAsync();
        var producto = await db.Producto.AsNoTracking().FirstOrDefaultAsync(item => item.IdProducto == id);
        if (producto is null) return Missing;
        model.Descripcion = model.Descripcion?.Trim() ?? string.Empty;
        model.CodigoBarras = string.IsNullOrWhiteSpace(model.CodigoBarras) ? null : model.CodigoBarras.Trim();
        if (producto.EsServicio || producto.IdUnidadMedidaBase is null)
            return Invalid(string.Empty, "Solo los artículos con unidad base pueden tener presentaciones.");
        var unidad = await db.UnidadesMedida.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdUnidadMedida == model.IdUnidadMedida && item.Activo);
        if (unidad is null)
            return Invalid(nameof(model.IdUnidadMedida), "Selecciona una unidad de medida activa.");
        if (model.Factor <= 0 || decimal.Round(model.Factor, 3) != model.Factor)
            return Invalid(nameof(model.Factor), "El factor debe ser positivo y admitir como máximo tres decimales.");
        if (!string.IsNullOrWhiteSpace(model.Descripcion) && await db.PresentacionesProducto.AnyAsync(item =>
            item.IdProducto == id && item.Descripcion == model.Descripcion))
            return Invalid(nameof(model.Descripcion), "Ya existe una presentación con ese nombre para el producto.");
        var presentacion = new PresentacionProducto
        {
            IdProducto = id, IdUnidadMedida = model.IdUnidadMedida, Descripcion = model.Descripcion,
            Factor = model.Factor, CodigoBarras = model.CodigoBarras, Activo = true
        };
        db.PresentacionesProducto.Add(presentacion);
        await db.SaveChangesAsync();
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "PresentacionProducto", presentacion.IdPresentacionProducto,
            "Presentación creada", null, presentacion.Descripcion,
            $"Producto {producto.Nombre}; factor {presentacion.Factor:0.###} {unidad.Descripcion} por presentación."));
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(ResultadoProductoEstado.Exito);
    }

    public async Task<ResultadoProducto> CambiarEstadoPresentacionAsync(int id, int presentacionId, ClaimsPrincipal user)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        var presentacion = await db.PresentacionesProducto.FirstOrDefaultAsync(item =>
            item.IdPresentacionProducto == presentacionId && item.IdProducto == id);
        if (presentacion is null) return Missing;
        var producto = await db.Producto.AsNoTracking().FirstOrDefaultAsync(item => item.IdProducto == id);
        if (producto is null) return Missing;
        if (presentacion.Activo && await db.PresentacionesProducto.CountAsync(item => item.IdProducto == id && item.Activo) <= 1)
            return Invalid(string.Empty, "El producto debe conservar al menos una presentación activa.");
        presentacion.Activo = !presentacion.Activo;
        await db.SaveChangesAsync();
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "PresentacionProducto", presentacion.IdPresentacionProducto,
            presentacion.Activo ? "Presentación activada" : "Presentación desactivada",
            presentacion.Activo ? "Inactiva" : "Activa", presentacion.Activo ? "Activa" : "Inactiva",
            $"Producto {producto.Nombre}."));
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(ResultadoProductoEstado.Exito, producto);
    }

    public async Task<ResultadoProducto> ActualizarAsync(int id, Producto producto)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await inventario.BloquearInventarioAsync();
        if (producto.RowVersion is null || producto.RowVersion.Length == 0)
            return Invalid(string.Empty, "La versión del producto no es válida. Recarga el formulario antes de guardar.");
        var tipoActual = await db.Producto.AsNoTracking().Where(item => item.IdProducto == id)
            .Select(item => new { item.EsServicio, item.IdUnidadMedidaBase, item.Stock }).FirstOrDefaultAsync();
        if (tipoActual is null) return Missing;
        producto.Stock = tipoActual.Stock;
        if (producto.EsServicio) producto.IdUnidadMedidaBase = null;
        if (tipoActual.EsServicio != producto.EsServicio && await db.DetalleVenta.AnyAsync(line => line.IdProducto == id))
            return Invalid(nameof(Producto.EsServicio), "No se puede cambiar el tipo de un producto que ya figura en ventas; registra uno nuevo.");
        if (tipoActual.EsServicio != producto.EsServicio &&
            (await db.SaldosAlmacen.AnyAsync(item => item.IdProducto == id)
             || await db.PresentacionesProducto.AnyAsync(item => item.IdProducto == id)))
            return Invalid(nameof(Producto.EsServicio), "No se puede cambiar el tipo de un artículo con presentación o saldo asociado.");
        var unidadBase = producto.EsServicio || producto.IdUnidadMedidaBase is null ? null
            : await db.UnidadesMedida.AsNoTracking().FirstOrDefaultAsync(item =>
                item.IdUnidadMedida == producto.IdUnidadMedidaBase && item.Activo);
        if (!producto.EsServicio && unidadBase is null)
            return Invalid(nameof(producto.IdUnidadMedidaBase), "Selecciona una unidad base activa y revisada para el artículo.");
        var cambioUnidad = tipoActual.IdUnidadMedidaBase != producto.IdUnidadMedidaBase;
        if (cambioUnidad && (tipoActual.Stock is > 0 || await TieneHistorialInventarioOComercialAsync(id)))
            return Invalid(nameof(producto.IdUnidadMedidaBase),
                "No se puede cambiar la unidad base después de registrar documentos comerciales o movimientos de inventario. Registra un producto nuevo o solicita una conversión controlada.");
        try
        {
            if (!producto.EsServicio && unidadBase is not null && cambioUnidad
                && !await SincronizarPresentacionBaseAsync(id, tipoActual.IdUnidadMedidaBase, unidadBase))
                return Invalid(nameof(producto.IdUnidadMedidaBase),
                    "No se pudo cambiar la unidad base porque hay presentaciones adicionales que requieren revisión manual.");
            db.Producto.Update(producto);
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync();
            var latest = await db.Producto.AsNoTracking().FirstOrDefaultAsync(item => item.IdProducto == id);
            return latest is null ? Missing : new(ResultadoProductoEstado.Conflicto, latest);
        }
        return new(ResultadoProductoEstado.Exito, producto);
    }

    public async Task<ResultadoProducto> EliminarAsync(int id)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await inventario.BloquearInventarioAsync();
        var producto = await db.Producto.FindAsync(id);
        if (producto is null) return Missing;
        if (await db.Set<DetalleCotizacion>().AnyAsync(d => d.IdProducto == id) ||
            await db.Set<DetallePedido>().AnyAsync(d => d.IdProducto == id) ||
            await db.DetalleVenta.AnyAsync(d => d.IdProducto == id) ||
            await db.SaldosAlmacen.AnyAsync(d => d.IdProducto == id) ||
            await db.DetallesCorteInventario.AnyAsync(d => d.IdProducto == id) ||
            await db.Kardex.AnyAsync(d => d.IdProducto == id) ||
            await db.PresentacionesProducto.AnyAsync(d => d.IdProducto == id))
        {
            await db.Entry(producto).Reference(p => p.IdProveedorNavigation).LoadAsync();
            return new(ResultadoProductoEstado.EnUso, producto);
        }
        db.Producto.Remove(producto);
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(ResultadoProductoEstado.Exito);
    }

    private async Task<bool> TieneHistorialInventarioOComercialAsync(int id) =>
        await db.DetalleCotizacion.AnyAsync(item => item.IdProducto == id)
        || await db.DetallePedido.AnyAsync(item => item.IdProducto == id)
        || await db.DetalleVenta.AnyAsync(item => item.IdProducto == id)
        || await db.DetallesInventario.AnyAsync(item => item.IdProducto == id)
        || await db.DetallesCorteInventario.AnyAsync(item => item.IdProducto == id)
        || await db.Kardex.AnyAsync(item => item.IdProducto == id)
        || await db.TransferenciasAlmacen.AnyAsync(item => item.IdProducto == id)
        || await db.SaldosAlmacen.AnyAsync(item => item.IdProducto == id && item.CantidadBase > 0);

    private async Task<bool> SincronizarPresentacionBaseAsync(int id, int? unidadAnteriorId, UnidadMedida unidadNueva)
    {
        var presentaciones = await db.PresentacionesProducto.Where(item => item.IdProducto == id).ToListAsync();
        if (presentaciones.Count == 0)
        {
            db.PresentacionesProducto.Add(new PresentacionProducto
            {
                IdProducto = id, IdUnidadMedida = unidadNueva.IdUnidadMedida,
                Descripcion = unidadNueva.DescripcionCorta ?? unidadNueva.Descripcion, Factor = 1m, Activo = true
            });
            return true;
        }
        if (unidadAnteriorId is null || presentaciones.Count != 1) return false;
        var basePresentation = presentaciones[0];
        if (basePresentation.IdUnidadMedida != unidadAnteriorId || basePresentation.Factor != 1m) return false;
        basePresentation.IdUnidadMedida = unidadNueva.IdUnidadMedida;
        basePresentation.Descripcion = unidadNueva.DescripcionCorta ?? unidadNueva.Descripcion;
        basePresentation.Activo = true;
        return true;
    }
}
