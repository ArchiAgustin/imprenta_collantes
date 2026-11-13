using System.Data;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class CotizacionService(
    ApplicationDbContext context,
    SerieDocumentoInternoService series) : ICotizacionService
{
    private const int MaxLineas = 100;
    private const decimal MaximoTotal = 99_999_999.99m;

    public Task<List<Cotizacion>> ListarAsync() => context.Cotizacion
        .AsNoTracking()
        .Include(item => item.IdClienteNavigation)
        .Include(item => item.IdUsuarioNavigation)
        .Include(item => item.DetalleCotizacions)
        .OrderByDescending(item => item.FechaCreacion)
        .ThenByDescending(item => item.IdCotizacion)
        .ToListAsync();

    public Task<Cotizacion?> ObtenerConLineasAsync(int id) => context.Cotizacion
        .AsNoTracking()
        .Include(item => item.IdClienteNavigation)
        .Include(item => item.IdUsuarioNavigation)
        .Include(item => item.DetalleCotizacions)
            .ThenInclude(linea => linea.IdProductoNavigation)
        .Include(item => item.Pedidos)
        .FirstOrDefaultAsync(item => item.IdCotizacion == id);

    public Task<Cotizacion?> ObtenerParaEdicionAsync(int id) => context.Cotizacion
        .Include(item => item.DetalleCotizacions)
        .Include(item => item.IdUsuarioNavigation)
        .FirstOrDefaultAsync(item => item.IdCotizacion == id);

    public Task<List<Almacen>> ObtenerAlmacenesVentaAsync() => context.Almacenes.AsNoTracking()
        .Where(item => item.Activo && item.FechaActivacionUtc != null)
        .OrderByDescending(item => item.EsPredeterminado)
        .ThenBy(item => item.Nombre)
        .ToListAsync();

    public Task<bool> TieneVentaAsync(int id) => context.Venta.AnyAsync(item => item.IdCotizacion == id);

    public async Task<bool> TieneVentaOPedidoAsync(int id) =>
        await context.Venta.AnyAsync(item => item.IdCotizacion == id)
        || await context.Pedido.AnyAsync(item => item.IdCotizacion == id);

    public async Task<List<CotizacionFormError>> ValidarFormularioAsync(CotizacionFormViewModel model)
    {
        var errors = new List<CotizacionFormError>();
        if (model.ClienteId is null || !await context.Cliente.AnyAsync(item => item.IdCliente == model.ClienteId))
            errors.Add(new(nameof(model.ClienteId), "Selecciona un cliente registrado."));

        model.Lineas ??= new List<CotizacionLineaInputViewModel>();
        if (model.Lineas.Count == 0)
            errors.Add(new(nameof(model.Lineas), "Agrega al menos un producto a la cotización."));
        else if (model.Lineas.Count > MaxLineas)
            errors.Add(new(nameof(model.Lineas), $"Una cotización admite hasta {MaxLineas} líneas."));

        var requestedIds = model.Lineas.Where(line => line?.ProductoId is not null)
            .Select(line => line.ProductoId!.Value).Distinct().ToArray();
        var products = await context.Producto.AsNoTracking()
            .Include(item => item.UnidadMedidaBase)
            .Include(item => item.Presentaciones.Where(presentation => presentation.Activo && presentation.UnidadMedida.Activo))
                .ThenInclude(presentation => presentation.UnidadMedida)
            .Where(item => requestedIds.Contains(item.IdProducto))
            .ToDictionaryAsync(item => item.IdProducto);

        for (var index = 0; index < model.Lineas.Count; index++)
        {
            var line = model.Lineas[index];
            if (line is null || line.ProductoId is not { } productId || !products.TryGetValue(productId, out var product))
            {
                errors.Add(new($"{nameof(model.Lineas)}[{index}].{nameof(CotizacionLineaInputViewModel.ProductoId)}", "El producto seleccionado ya no existe."));
                continue;
            }

            if (product.EsServicio)
            {
                if (line.IdPresentacionProducto is not null)
                    errors.Add(new($"{nameof(model.Lineas)}[{index}].{nameof(CotizacionLineaInputViewModel.IdPresentacionProducto)}", "Los servicios no requieren presentación de inventario."));
                line.IdPresentacionProducto = null;
                line.FactorPresentacion = 1m;
                line.PresentacionDescripcion = "Servicio";
                line.UnidadBaseDescripcion = null;
            }
            else if (line.IdPresentacionProducto is { } selectedId
                && !product.Presentaciones.Any(item => item.IdPresentacionProducto == selectedId))
            {
                errors.Add(new($"{nameof(model.Lineas)}[{index}].{nameof(CotizacionLineaInputViewModel.IdPresentacionProducto)}", "Selecciona una presentación activa para este artículo."));
            }
            else
            {
                var presentation = line.IdPresentacionProducto is { } idPresentation
                    ? product.Presentaciones.FirstOrDefault(item => item.IdPresentacionProducto == idPresentation)
                    : null;
                presentation ??= product.Presentaciones.FirstOrDefault(item =>
                    item.IdUnidadMedida == product.IdUnidadMedidaBase && item.Factor == 1m);
                if (presentation is null)
                {
                    line.IdPresentacionProducto = null;
                    line.FactorPresentacion = 1m;
                    line.PresentacionDescripcion = product.UnidadMedidaBase?.DescripcionCorta ?? product.UnidadMedidaBase?.Descripcion ?? "Unidad base";
                    line.UnidadBaseDescripcion = product.UnidadMedidaBase?.Descripcion;
                }
                else
                {
                    line.IdPresentacionProducto = presentation.IdPresentacionProducto;
                    line.FactorPresentacion = presentation.Factor;
                    line.PresentacionDescripcion = presentation.Descripcion;
                    line.UnidadBaseDescripcion = product.UnidadMedidaBase?.Descripcion;
                }
            }

            var factor = line.FactorPresentacion > 0m ? line.FactorPresentacion : 1m;
            var minimum = SugerenciaPrecioService.CalcularMinimo(product.CostoUnitarioSinIgv is { } cost ? cost * factor : null);
            if (minimum is { } floor && line.PrecioUnitario < floor)
                errors.Add(new($"{nameof(model.Lineas)}[{index}].{nameof(CotizacionLineaInputViewModel.PrecioUnitario)}",
                    $"El precio por presentación sin IGV debe ser al menos {floor.ToString("C2", CultureInfo.GetCultureInfo("es-PE"))} para conservar un margen mínimo del 10 %."));
        }

        return errors;
    }

    public async Task CargarRecursosFormularioAsync(CotizacionFormViewModel model)
    {
        model.Clientes = await context.Cliente.AsNoTracking()
            .OrderBy(item => item.Nombre)
            .ToListAsync();
        model.Productos = await ObtenerProductosAsync();
        var productsById = model.Productos.ToDictionary(item => item.IdProducto);
        foreach (var line in (model.Lineas ?? new()).Where(item => item.ProductoId is not null))
        {
            if (!productsById.TryGetValue(line.ProductoId!.Value, out var product)) continue;
            if (product.EsServicio)
            {
                line.IdPresentacionProducto = null;
                line.FactorPresentacion = 1m;
                line.PresentacionDescripcion = "Servicio";
                line.UnidadBaseDescripcion = null;
                continue;
            }

            var selected = line.IdPresentacionProducto is { } presentationId
                ? product.Presentaciones.FirstOrDefault(item => item.IdPresentacionProducto == presentationId)
                : null;
            selected ??= line.IdPresentacionProducto is null
                ? product.Presentaciones.FirstOrDefault(item => item.IdUnidadMedida == product.IdUnidadMedidaBase && item.Factor == 1m)
                : null;
            if (selected is not null)
            {
                line.IdPresentacionProducto = selected.IdPresentacionProducto;
                line.FactorPresentacion = selected.Factor;
                line.PresentacionDescripcion = selected.Descripcion;
            }
            else if (line.IdPresentacionProducto is null)
            {
                line.FactorPresentacion = 1m;
                line.PresentacionDescripcion = product.UnidadMedidaBase?.DescripcionCorta ?? product.UnidadMedidaBase?.Descripcion ?? "Unidad base";
            }
            line.UnidadBaseDescripcion = product.UnidadMedidaBase?.Descripcion;
        }
    }

    public async Task<ResultadoOperacionCotizacion> CrearAsync(
        CotizacionFormViewModel model,
        string userId,
        ClaimsPrincipal user)
    {
        Cotizacion? original = null;
        if (model.IdCotizacionOrigen is { } originId)
        {
            original = await context.Cotizacion.AsNoTracking().FirstOrDefaultAsync(item => item.IdCotizacion == originId);
            if (original is null || original.Estado != "Vencida" || await TieneVentaAsync(originId))
                return new(EstadoOperacionCotizacion.Conflicto, "La cotización de origen ya no está vencida o tiene una venta. Recarga y revisa su estado.");
        }

        var cliente = await context.Cliente.AsNoTracking().FirstOrDefaultAsync(item => item.IdCliente == model.ClienteId);
        if (cliente is null)
            return new(EstadoOperacionCotizacion.DatosNoDisponibles, "El cliente seleccionado ya no está disponible. Recarga el formulario.");
        var productIds = model.Lineas.Select(line => line.ProductoId!.Value).Distinct().ToArray();
        var productNames = await context.Producto.AsNoTracking()
            .Where(item => productIds.Contains(item.IdProducto))
            .ToDictionaryAsync(item => item.IdProducto, item => item.Nombre);
        if (productNames.Count != productIds.Length)
            return new(EstadoOperacionCotizacion.DatosNoDisponibles, "Uno de los productos seleccionados ya no está disponible. Recarga el formulario.");

        var cotizacion = new Cotizacion
        {
            IdCliente = model.ClienteId,
            IdUsuario = userId,
            IdCotizacionOrigen = original?.IdCotizacion,
            FechaCreacion = FechaComercial.HoyPeru(),
            FechaVencimiento = model.FechaVencimiento,
            DetalleCotizacion = model.Descripcion?.Trim(),
            Total = model.Totales.Total,
            ClienteNombreEmision = cliente.Nombre,
            ClienteDocumentoEmision = cliente.NumeroDocumento ?? cliente.Dni,
            ClienteDireccionEmision = cliente.Direccion
        };
        foreach (var line in model.Lineas)
            cotizacion.DetalleCotizacions.Add(ToEntity(line, productNames[line.ProductoId!.Value]));

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync()
            : null;
        var number = await series.AsignarAsync(SerieDocumentoInternoService.Cotizacion);
        cotizacion.SerieInterna = number.Serie;
        cotizacion.CorrelativoInterno = number.Correlativo;
        cotizacion.DigitosNumeroInterno = number.Digitos;
        context.Cotizacion.Add(cotizacion);
        await context.SaveChangesAsync();
        context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "Cotizacion", cotizacion.IdCotizacion,
            original is null ? "Cotización emitida" : "Nueva cotización tras revalidación", null, cotizacion.Estado,
            original is null ? null : $"Origen vencido #{original.IdCotizacion}; revisión humana, original conservado."));
        await context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(EstadoOperacionCotizacion.Exito, $"Se emitió la cotización {cotizacion.NumeroInterno} con sus líneas e importes calculados.", cotizacion.IdCotizacion);
    }

    public async Task<ResultadoOperacionCotizacion> ActualizarAsync(int id, CotizacionFormViewModel model, ClaimsPrincipal user)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;
        var cotizacion = await context.Cotizacion
            .Include(item => item.DetalleCotizacions)
            .Include(item => item.IdUsuarioNavigation)
            .FirstOrDefaultAsync(item => item.IdCotizacion == id);
        if (cotizacion is null)
            return new(EstadoOperacionCotizacion.NoEncontrada, "No se encontró la cotización.");
        if (cotizacion.Estado != "Emitida")
            return new(EstadoOperacionCotizacion.Conflicto, "Solo se pueden editar cotizaciones emitidas y vigentes.");

        if (transaction is not null)
        {
            var claimed = await context.Cotizacion
                .Where(item => item.IdCotizacion == id && item.Estado == "Emitida"
                    && (item.FechaVencimiento == null || item.FechaVencimiento >= FechaComercial.HoyPeru()))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Estado, "Emitida"));
            if (claimed != 1)
                return new(EstadoOperacionCotizacion.Conflicto, "La cotización venció o fue convertida mientras la editabas. No se guardaron los cambios.");
        }

        var cliente = await context.Cliente.AsNoTracking().FirstOrDefaultAsync(item => item.IdCliente == model.ClienteId);
        if (cliente is null)
            return new(EstadoOperacionCotizacion.DatosNoDisponibles, "El cliente seleccionado ya no está disponible. Recarga el formulario.");
        var productIds = model.Lineas.Select(line => line.ProductoId!.Value).Distinct().ToArray();
        var productNames = await context.Producto.AsNoTracking()
            .Where(item => productIds.Contains(item.IdProducto))
            .ToDictionaryAsync(item => item.IdProducto, item => item.Nombre);
        if (productNames.Count != productIds.Length)
            return new(EstadoOperacionCotizacion.DatosNoDisponibles, "Uno de los productos seleccionados ya no está disponible. Recarga el formulario.");

        cotizacion.IdCliente = model.ClienteId;
        cotizacion.ClienteNombreEmision = cliente.Nombre;
        cotizacion.ClienteDocumentoEmision = cliente.NumeroDocumento ?? cliente.Dni;
        cotizacion.ClienteDireccionEmision = cliente.Direccion;
        cotizacion.FechaVencimiento = model.FechaVencimiento;
        cotizacion.DetalleCotizacion = model.Descripcion?.Trim();
        cotizacion.Total = model.Totales.Total;
        context.DetalleCotizacion.RemoveRange(cotizacion.DetalleCotizacions);
        cotizacion.DetalleCotizacions.Clear();
        foreach (var line in model.Lineas)
            cotizacion.DetalleCotizacions.Add(ToEntity(line, productNames[line.ProductoId!.Value]));

        await context.SaveChangesAsync();
        context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "Cotizacion", id,
            "Cotización actualizada", "Emitida", "Emitida"));
        await context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(EstadoOperacionCotizacion.Exito, $"Se actualizaron la cotización #{id}, sus líneas y sus importes.", id);
    }

    public async Task<ResultadoOperacionCotizacion> EliminarAsync(int id, ClaimsPrincipal user)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;
        var cotizacion = await context.Cotizacion
            .Include(item => item.DetalleCotizacions)
            .FirstOrDefaultAsync(item => item.IdCotizacion == id);
        if (cotizacion is null)
            return new(EstadoOperacionCotizacion.NoEncontrada, "No se encontró la cotización.");
        if (cotizacion.Estado != "Emitida")
            return new(EstadoOperacionCotizacion.Conflicto, "Solo se pueden eliminar cotizaciones emitidas que no se hayan convertido en venta.");

        if (transaction is not null)
        {
            var claimed = await context.Cotizacion
                .Where(item => item.IdCotizacion == id && item.Estado == "Emitida"
                    && (item.FechaVencimiento == null || item.FechaVencimiento >= FechaComercial.HoyPeru()))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Estado, "Emitida"));
            if (claimed != 1)
                return new(EstadoOperacionCotizacion.Conflicto, "La cotización venció o fue convertida mientras la eliminabas.");
        }

        if (await TieneVentaOPedidoAsync(id))
            return new(EstadoOperacionCotizacion.Vinculada, "No se puede eliminar una cotización ya vinculada a una venta o pedido.");

        context.DetalleCotizacion.RemoveRange(cotizacion.DetalleCotizacions);
        context.Cotizacion.Remove(cotizacion);
        context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "Cotizacion", id,
            "Cotización eliminada", "Emitida", null));
        await context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(EstadoOperacionCotizacion.Exito, $"Se eliminó la cotización #{id} y sus líneas.", id);
    }

    public async Task RegistrarEnvioPorCorreoAsync(int id, ClaimsPrincipal user)
    {
        var cotizacion = await context.Cotizacion.AsNoTracking()
            .Where(item => item.IdCotizacion == id)
            .Select(item => new { item.Estado })
            .SingleAsync();
        context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "Cotizacion", id,
            "Cotización enviada por correo", cotizacion.Estado, cotizacion.Estado, "PDF comercial adjunto."));
        await context.SaveChangesAsync();
    }

    private async Task<List<Producto>> ObtenerProductosAsync() => await context.Producto.AsNoTracking()
        .Include(item => item.UnidadMedidaBase)
        .Include(item => item.Presentaciones.Where(presentation => presentation.Activo && presentation.UnidadMedida.Activo))
            .ThenInclude(presentation => presentation.UnidadMedida)
        .OrderBy(item => item.Nombre)
        .ToListAsync();

    private static DetalleCotizacion ToEntity(CotizacionLineaInputViewModel line, string? name) => new()
    {
        IdProducto = line.ProductoId,
        ProductoNombreEmision = name,
        IdPresentacionProducto = line.IdPresentacionProducto,
        FactorPresentacion = line.FactorPresentacion,
        PresentacionDescripcion = line.PresentacionDescripcion,
        UnidadBaseDescripcion = line.UnidadBaseDescripcion,
        Cantidad = line.Cantidad,
        PrecioUnitario = line.PrecioUnitario
    };
}
