using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ResultadoVenta(bool Exito, string Mensaje, int? IdVenta = null);

public sealed class VentaService(ApplicationDbContext context)
{
    public async Task<ResultadoVenta> ConfirmarCotizacionAsync(int cotizacionId, string userId, System.Security.Claims.ClaimsPrincipal user)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;

        var almacen = await new InventarioService(context).BloquearPrincipalAsync();
        if (almacen?.ConteoEnCurso == true)
            return await FailAsync(transaction, "Hay un conteo físico en curso. Las ventas están pausadas hasta aprobarlo o anularlo.");
        var inventarioActivo = almacen?.FechaActivacionUtc is not null;

        var today = FechaComercial.HoyPeru();
        if (transaction is not null)
        {
            // Claim this quote with an exclusive lock until commit. A second confirmation
            // or quote edit must wait, then sees the final state instead of stale lines.
            var claimed = await context.Cotizacion
                .Where(item => item.IdCotizacion == cotizacionId && item.Estado == "Emitida"
                    && (item.FechaVencimiento == null || item.FechaVencimiento >= today))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Estado, "Convertida"));
            if (claimed != 1)
            {
                var expired = await context.Cotizacion
                    .Where(item => item.IdCotizacion == cotizacionId && item.Estado == "Emitida"
                        && item.FechaVencimiento != null && item.FechaVencimiento < today)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Estado, "Vencida"));
                if (expired == 1)
                {
                    context.AuditoriaOperacion.Add(AuditoriaFactory.CrearSistema("Cotizacion", cotizacionId,
                        "Vencimiento automático", "Emitida", "Vencida"));
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return new ResultadoVenta(false, "La cotización venció y ya no puede convertirse en venta.");
                }
                return await FailAsync(transaction, "La cotización no está emitida y vigente, o ya fue convertida en venta.");
            }
        }

        var quoteQuery = context.Cotizacion.Include(item => item.DetalleCotizacions);
        var quote = transaction is null
            ? await quoteQuery.FirstOrDefaultAsync(item => item.IdCotizacion == cotizacionId)
            : await quoteQuery.AsNoTracking().FirstOrDefaultAsync(item => item.IdCotizacion == cotizacionId);
        if (quote is null) return await FailAsync(transaction, "No se encontró la cotización.");

        if (transaction is null && quote.Estado == "Emitida" && quote.FechaVencimiento is { } dueDate && dueDate < today)
        {
            quote.Estado = "Vencida";
            context.AuditoriaOperacion.Add(AuditoriaFactory.CrearSistema("Cotizacion", quote.IdCotizacion,
                "Vencimiento automático", "Emitida", "Vencida"));
            await context.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return new ResultadoVenta(false, "La cotización venció y ya no puede convertirse en venta.");
        }

        if (quote.Estado != (transaction is null ? "Emitida" : "Convertida"))
            return await FailAsync(transaction, "Solo se puede confirmar una cotización emitida y vigente.");
        if (await context.Venta.AnyAsync(item => item.IdCotizacion == quote.IdCotizacion))
            return await FailAsync(transaction, "Esta cotización ya fue convertida en venta.");
        if (quote.IdCliente is null || quote.DetalleCotizacions.Count == 0)
            return await FailAsync(transaction, "La cotización debe tener cliente y al menos una línea.");
        var cliente = await context.Cliente.AsNoTracking().FirstOrDefaultAsync(item => item.IdCliente == quote.IdCliente);
        if (cliente is null) return await FailAsync(transaction, "No se encontró el cliente de la cotización.");

        if (quote.DetalleCotizacions.Any(line => !line.IdProducto.HasValue))
            return await FailAsync(transaction, "Una línea de la cotización ya no tiene un producto asociado.");

        var productIds = quote.DetalleCotizacions
            .Select(line => line.IdProducto!.Value)
            .Distinct()
            .ToArray();

        var products = await context.Producto.AsNoTracking()
            .Where(product => productIds.Contains(product.IdProducto))
            .ToDictionaryAsync(product => product.IdProducto);
        if (products.Count != productIds.Length)
            return await FailAsync(transaction, "Uno de los productos de la cotización ya no existe.");

        var quantitiesByProduct = quote.DetalleCotizacions
            .GroupBy(line => line.IdProducto!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Cantidad ?? 0));
        var saldos = inventarioActivo
            ? await context.SaldosAlmacen.AsNoTracking()
                .Where(item => item.IdAlmacen == almacen!.IdAlmacen && productIds.Contains(item.IdProducto))
                .ToDictionaryAsync(item => item.IdProducto)
            : new Dictionary<int, StockAlmacen>();
        foreach (var line in quote.DetalleCotizacions)
        {
            if (line.Cantidad is null or <= 0 || line.PrecioUnitario is null or < 0)
                return await FailAsync(transaction, "La cotización contiene cantidades o precios inválidos.");
            var product = products[line.IdProducto!.Value];
            var floor = SugerenciaPrecioService.CalcularMinimo(product.CostoUnitarioSinIgv);
            if (floor is { } minimum && line.PrecioUnitario < minimum)
                return await FailAsync(transaction,
                    $"El costo de «{product.Nombre}» exige al menos {minimum.ToString("C2", CultureInfo.GetCultureInfo("es-PE"))} por unidad sin IGV para conservar el margen mínimo del 10 %. Revisa la cotización antes de confirmar la venta.");
        }

        foreach (var (productId, quantity) in quantitiesByProduct)
        {
            var product = products[productId];
            if (!product.EsServicio && inventarioActivo &&
                (!saldos.TryGetValue(productId, out var saldo) || saldo.CantidadBase < quantity))
                return await FailAsync(transaction, $"Stock insuficiente para «{product.Nombre}» en el Almacén principal. La cotización se conserva sin cambios.");
            if (!product.EsServicio && !inventarioActivo && (product.Stock ?? 0) < quantity)
                return await FailAsync(transaction, $"Stock insuficiente para «{product.Nombre}». La cotización se conserva sin cambios.");
        }

        foreach (var (productId, quantity) in quantitiesByProduct)
        {
            var product = products[productId];
            if (product.EsServicio) continue;

            if (inventarioActivo)
            {
                if (context.Database.IsRelational())
                {
                    var snapshot = saldos[productId];
                    var updated = await context.SaldosAlmacen
                        .Where(item => item.IdAlmacen == almacen!.IdAlmacen && item.IdProducto == productId
                            && item.CantidadBase >= quantity && item.RowVersion == snapshot.RowVersion)
                        .ExecuteUpdateAsync(update => update.SetProperty(item => item.CantidadBase, item => item.CantidadBase - quantity));
                    if (updated != 1)
                        return await FailAsync(transaction, $"El saldo de «{product.Nombre}» cambió durante la confirmación. Reintenta.");
                }
                else
                {
                    var tracked = await context.SaldosAlmacen.FindAsync(almacen!.IdAlmacen, productId);
                    if (tracked is null || tracked.CantidadBase < quantity)
                        return await FailAsync(transaction, $"Stock insuficiente para «{product.Nombre}».");
                    tracked.CantidadBase -= quantity;
                }
                continue;
            }

            if (context.Database.IsRelational())
            {
                var updated = await context.Producto
                    .Where(item => item.IdProducto == productId && !item.EsServicio
                        && item.Stock != null && item.Stock >= quantity && item.RowVersion == product.RowVersion)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Stock, item => item.Stock - quantity));
                if (updated != 1)
                    return await FailAsync(transaction, $"El stock o el costo de «{product.Nombre}» cambió durante la confirmación. Inténtalo nuevamente.");
            }
            else
            {
                var trackedProduct = await context.Producto.FindAsync(productId);
                if (trackedProduct is null || trackedProduct.EsServicio || (trackedProduct.Stock ?? 0) < quantity)
                    return await FailAsync(transaction, $"Stock insuficiente para «{product.Nombre}». La cotización se conserva sin cambios.");
                trackedProduct.Stock = (trackedProduct.Stock ?? 0) - quantity;
            }
        }

        var subtotal = quote.DetalleCotizacions.Sum(line => decimal.Round(line.Cantidad!.Value * line.PrecioUnitario!.Value, 2, MidpointRounding.AwayFromZero));
        var igv = decimal.Round(subtotal * .18m, 2, MidpointRounding.AwayFromZero);
        if (quote.Total != subtotal + igv)
            return await FailAsync(transaction, "Los importes de la cotización no concuerdan con sus líneas. Revísala antes de confirmar.");

        var sale = new Venta
        {
            IdCotizacion = quote.IdCotizacion,
            IdCliente = quote.IdCliente.Value,
            IdUsuario = userId,
            FechaVentaUtc = DateTimeOffset.UtcNow,
            Estado = EstadoVenta.Confirmada,
            Total = quote.Total!.Value,
            ClienteNombreEmision = cliente.Nombre,
            ClienteDocumentoEmision = cliente.NumeroDocumento ?? cliente.Dni,
            ClienteDireccionEmision = cliente.Direccion,
            SubtotalSinIgvEmision = subtotal,
            IgvEmision = igv
        };
        foreach (var line in quote.DetalleCotizacions.OrderBy(item => item.IdDetalleCotizacion))
        {
            var product = products[line.IdProducto!.Value];
            sale.Detalles.Add(new DetalleVenta
            {
                IdProducto = product.IdProducto,
                ProductoNombre = product.Nombre ?? "Producto",
                Cantidad = line.Cantidad!.Value,
                PrecioUnitario = line.PrecioUnitario!.Value,
                CostoUnitarioSinIgv = product.CostoUnitarioSinIgv,
                EsServicio = product.EsServicio
            });
        }

        var numeroVenta = await new SerieDocumentoInternoService(context).AsignarAsync(SerieDocumentoInternoService.Venta);
        sale.SerieInterna = numeroVenta.Serie;
        sale.CorrelativoInterno = numeroVenta.Correlativo;
        sale.DigitosNumeroInterno = numeroVenta.Digitos;
        context.Venta.Add(sale);
        await context.SaveChangesAsync();

        if (inventarioActivo)
            foreach (var (productId, quantity) in quantitiesByProduct.Where(item => !products[item.Key].EsServicio))
                context.Kardex.Add(new Kardex
                {
                    IdAlmacen = almacen!.IdAlmacen, IdProducto = productId, FechaUtc = sale.FechaVentaUtc,
                    TipoMovimiento = "Salida venta", CantidadBase = quantity,
                    SaldoAnteriorBase = saldos[productId].CantidadBase,
                    SaldoPosteriorBase = saldos[productId].CantidadBase - quantity,
                    TipoReferencia = "Venta", IdReferencia = sale.IdVenta, IdUsuario = userId
                });

        var order = new Pedido
        {
            IdVenta = sale.IdVenta,
            IdCotizacion = quote.IdCotizacion,
            IdCliente = quote.IdCliente,
            IdUsuario = userId,
            FechaPedido = today,
            EstadoPedido = EstadoPedido.EnCola,
            Total = sale.Total
        };
        foreach (var line in sale.Detalles)
        {
            order.DetallePedidos.Add(new DetallePedido
            {
                IdProducto = line.IdProducto,
                ProductoNombre = line.ProductoNombre,
                Cantidad = line.Cantidad,
                PrecioUnitario = line.PrecioUnitario,
                EsServicio = line.EsServicio
            });
        }

        context.Pedido.Add(order);
        if (transaction is null) quote.Estado = "Convertida";
        await context.SaveChangesAsync();

        context.AuditoriaOperacion.AddRange(
            AuditoriaFactory.Crear(user, "Cotizacion", quote.IdCotizacion, "Convertida en venta", "Emitida", "Convertida"),
            AuditoriaFactory.Crear(user, "Venta", sale.IdVenta, "Venta confirmada", null, EstadoVenta.Confirmada,
                $"Pedido #{order.IdPedido} creado y stock reservado."),
            AuditoriaFactory.Crear(user, "Pedido", order.IdPedido, "Trabajo creado", null, EstadoPedido.EnCola,
                $"Asociado a la venta #{sale.IdVenta}."));
        await context.SaveChangesAsync();

        if (transaction is not null) await transaction.CommitAsync();
        return new ResultadoVenta(true, $"Venta #{sale.IdVenta} confirmada y trabajo #{order.IdPedido} agregado a la cola.", sale.IdVenta);
    }

    public async Task<ResultadoVenta> CancelarAsync(int ventaId, System.Security.Claims.ClaimsPrincipal user)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;

        var almacen = await new InventarioService(context).BloquearPrincipalAsync();
        if (almacen?.ConteoEnCurso == true)
            return await FailAsync(transaction, "Hay un conteo físico en curso. Las cancelaciones están pausadas.");
        var inventarioActivo = almacen?.FechaActivacionUtc is not null;

        var saleQuery = context.Venta
            .Include(item => item.Detalles)
            .Include(item => item.Pedido);
        var sale = context.Database.IsRelational()
            ? await saleQuery.AsNoTracking().FirstOrDefaultAsync(item => item.IdVenta == ventaId)
            : await saleQuery.FirstOrDefaultAsync(item => item.IdVenta == ventaId);
        if (sale is null) return await FailAsync(transaction, "No se encontró la venta.");
        if (sale.Estado != EstadoVenta.Confirmada || sale.Pedido?.EstadoPedido != EstadoPedido.EnCola)
            return await FailAsync(transaction, "Solo se puede cancelar una venta mientras su trabajo siga En cola.");

        // Claim the queued work before touching inventory. A simultaneous operator move or
        // cancellation can only win once; the loser rolls back without changing stock.
        if (context.Database.IsRelational())
        {
            var claimed = await context.Pedido
                .Where(item => item.IdPedido == sale.Pedido.IdPedido && item.EstadoPedido == EstadoPedido.EnCola)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.EstadoPedido, EstadoPedido.Cancelado));
            if (claimed != 1)
                return await FailAsync(transaction, "El trabajo ya comenzó o fue cancelado; no se modificó el inventario.");

            var cancelled = await context.Venta
                .Where(item => item.IdVenta == ventaId && item.Estado == EstadoVenta.Confirmada)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Estado, EstadoVenta.Cancelada));
            if (cancelled != 1)
                return await FailAsync(transaction, "La venta ya cambió de estado; no se modificó el inventario.");
        }

        var productQuantities = sale.Detalles
            .Where(line => !line.EsServicio)
            .GroupBy(line => line.IdProducto)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Cantidad));

        foreach (var (productId, quantity) in productQuantities)
        {
            if (inventarioActivo)
            {
                var saldo = await context.SaldosAlmacen.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.IdAlmacen == almacen!.IdAlmacen && item.IdProducto == productId);
                if (saldo is null) return await FailAsync(transaction, "No existe saldo del artículo; se revirtió la cancelación.");
                if (saldo.CantidadBase + quantity > 999_999_999_999_999.999m)
                    return await FailAsync(transaction, "La existencia excedería el límite permitido; se revirtió la cancelación.");
                if (context.Database.IsRelational())
                {
                    var updated = await context.SaldosAlmacen
                        .Where(item => item.IdAlmacen == almacen!.IdAlmacen && item.IdProducto == productId && item.RowVersion == saldo.RowVersion)
                        .ExecuteUpdateAsync(update => update.SetProperty(item => item.CantidadBase, item => item.CantidadBase + quantity));
                    if (updated != 1) return await FailAsync(transaction, "El saldo cambió durante la cancelación; no se restituyó inventario.");
                }
                else
                {
                    var tracked = await context.SaldosAlmacen.FindAsync(almacen!.IdAlmacen, productId);
                    tracked!.CantidadBase += quantity;
                }
                context.Kardex.Add(new Kardex
                {
                    IdAlmacen = almacen!.IdAlmacen, IdProducto = productId, FechaUtc = DateTimeOffset.UtcNow,
                    TipoMovimiento = "Devolución", CantidadBase = quantity,
                    SaldoAnteriorBase = saldo.CantidadBase, SaldoPosteriorBase = saldo.CantidadBase + quantity,
                    TipoReferencia = "Venta", IdReferencia = sale.IdVenta,
                    IdUsuario = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                });
                continue;
            }
            if (context.Database.IsRelational())
            {
                var updated = await context.Producto
                    .Where(item => item.IdProducto == productId)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Stock, item => (item.Stock ?? 0) + quantity));
                if (updated != 1)
                    return await FailAsync(transaction, "No se pudo restituir el inventario; la cancelación fue revertida.");
            }
            else
            {
                var product = await context.Producto.FindAsync(productId);
                if (product is null)
                    return await FailAsync(transaction, "No se pudo restituir el inventario; la cancelación fue revertida.");
                product.Stock = (product.Stock ?? 0) + quantity;
            }
        }

        var previousSaleState = sale.Estado;
        var previousOrderState = sale.Pedido.EstadoPedido;
        if (!context.Database.IsRelational())
        {
            sale.Estado = EstadoVenta.Cancelada;
            sale.Pedido.EstadoPedido = EstadoPedido.Cancelado;
        }
        context.AuditoriaOperacion.AddRange(
            AuditoriaFactory.Crear(user, "Venta", sale.IdVenta, "Venta cancelada", previousSaleState, EstadoVenta.Cancelada,
                $"Inventario restituido. Pedido #{sale.Pedido.IdPedido}."),
            AuditoriaFactory.Crear(user, "Pedido", sale.Pedido.IdPedido, "Trabajo cancelado", previousOrderState, EstadoPedido.Cancelado,
                $"Cancelación de venta #{sale.IdVenta}."));
        await context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new ResultadoVenta(true, $"Venta #{sale.IdVenta} cancelada y el stock restituido.", sale.IdVenta);
    }

    public async Task<ResultadoVenta> AvanzarPedidoAsync(int pedidoId, string nuevoEstado, System.Security.Claims.ClaimsPrincipal user)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;

        var orderQuery = context.Pedido.Include(item => item.IdVentaNavigation);
        var order = context.Database.IsRelational()
            ? await orderQuery.AsNoTracking().FirstOrDefaultAsync(item => item.IdPedido == pedidoId)
            : await orderQuery.FirstOrDefaultAsync(item => item.IdPedido == pedidoId);
        if (order is null) return await FailAsync(transaction, "No se encontró el trabajo.");

        if (order.IdVenta is not null && order.IdVentaNavigation?.Estado != EstadoVenta.Confirmada)
            return await FailAsync(transaction, "La venta de este trabajo no está confirmada.");

        var currentIndex = Array.IndexOf(EstadoPedido.FlujoProduccion.ToArray(), order.EstadoPedido ?? string.Empty);
        if (currentIndex < 0 || currentIndex >= EstadoPedido.FlujoProduccion.Count - 1
            || EstadoPedido.FlujoProduccion[currentIndex + 1] != nuevoEstado)
            return await FailAsync(transaction, "El trabajo solo puede avanzar al siguiente estado del flujo.");

        var previousState = order.EstadoPedido;
        if (context.Database.IsRelational())
        {
            var advanced = await context.Pedido
                .Where(item => item.IdPedido == pedidoId && item.EstadoPedido == previousState)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.EstadoPedido, nuevoEstado));
            if (advanced != 1)
                return await FailAsync(transaction, "El trabajo cambió de estado; recarga la página antes de continuar.");
        }
        else
        {
            order.EstadoPedido = nuevoEstado;
        }
        context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "Pedido", order.IdPedido,
            "Estado de trabajo actualizado", previousState, nuevoEstado,
            order.IdVenta is null ? null : $"Venta #{order.IdVenta}."));
        await context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new ResultadoVenta(true, $"El trabajo #{order.IdPedido} avanzó a «{nuevoEstado}».", order.IdVenta);
    }

    private static async Task<ResultadoVenta> FailAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction, string message)
    {
        if (transaction is not null) await transaction.RollbackAsync();
        return new ResultadoVenta(false, message);
    }
}
