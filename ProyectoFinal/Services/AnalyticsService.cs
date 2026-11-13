using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class AnalyticsService(ApplicationDbContext context) : IAnalyticsService
{
    private static readonly TimeSpan HorarioPeru = TimeSpan.FromHours(-5);

    public async Task<AnalyticsDashboardViewModel> ObtenerAsync(DateOnly desde, DateOnly hasta, int umbralStock)
    {
        var inicio = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), HorarioPeru).ToUniversalTime();
        var fin = new DateTimeOffset(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), HorarioPeru).ToUniversalTime();
        var resumen = new AnalyticsDashboardViewModel { Desde = desde, Hasta = hasta, UmbralStock = umbralStock };

        var ventas = await context.Venta.AsNoTracking()
            .Where(venta => venta.FechaVentaUtc >= inicio && venta.FechaVentaUtc < fin)
            .Select(venta => new
            {
                venta.IdVenta,
                venta.IdCliente,
                Cliente = venta.Cliente == null ? null : venta.Cliente.Nombre,
                venta.Estado,
                venta.Total,
                venta.FechaVentaUtc
            })
            .ToListAsync();

        resumen.VentasCanceladas = ventas.Count(venta => venta.Estado == EstadoVenta.Cancelada);
        var confirmadas = ventas.Where(venta => venta.Estado == EstadoVenta.Confirmada).ToList();
        resumen.VentasConfirmadas = confirmadas.Count;
        resumen.VentasConIgv = confirmadas.Sum(venta => venta.Total);

        var idsVentas = confirmadas.Select(venta => venta.IdVenta).ToArray();
        var lineas = await context.DetalleVenta.AsNoTracking()
            .Where(linea => idsVentas.Contains(linea.IdVenta))
            .Select(linea => new
            {
                linea.IdVenta,
                linea.IdDetalleVenta,
                linea.IdProducto,
                linea.ProductoNombre,
                linea.Cantidad,
                linea.PrecioUnitario,
                linea.CostoUnitarioSinIgv,
                linea.FactorPresentacion
            })
            .ToListAsync();
        var lineasPorVenta = lineas.GroupBy(linea => linea.IdVenta)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.OrderBy(linea => linea.IdDetalleVenta).ToList());

        var ingresosPorProducto = new Dictionary<int, (string Nombre, decimal Unidades, decimal Total)>();
        foreach (var venta in confirmadas)
        {
            if (!lineasPorVenta.TryGetValue(venta.IdVenta, out var detalle))
                detalle = lineas.Take(0).ToList();
            var subtotalVenta = detalle.Sum(linea => linea.Cantidad * linea.PrecioUnitario);
            resumen.VentasSinIgv += subtotalVenta;
            var igvPendiente = venta.Total - subtotalVenta;

            for (var index = 0; index < detalle.Count; index++)
            {
                var linea = detalle[index];
                var neto = linea.Cantidad * linea.PrecioUnitario;
                var cantidadBase = linea.Cantidad * linea.FactorPresentacion;
                var igvLinea = index == detalle.Count - 1
                    ? igvPendiente
                    : subtotalVenta == 0 ? 0 : decimal.Round((venta.Total - subtotalVenta) * neto / subtotalVenta, 2, MidpointRounding.AwayFromZero);
                igvPendiente -= igvLinea;
                resumen.TotalLineas++;
                if (linea.CostoUnitarioSinIgv is { } costo)
                {
                    resumen.CostoConocidoSinIgv += cantidadBase * costo;
                    resumen.IngresoNetoConCostoConocido += neto;
                }
                else resumen.LineasSinCosto++;

                ingresosPorProducto.TryGetValue(linea.IdProducto, out var acumulado);
                ingresosPorProducto[linea.IdProducto] = (
                    linea.ProductoNombre,
                    acumulado.Unidades + cantidadBase,
                    acumulado.Total + neto + igvLinea);
            }
        }
        resumen.Igv = resumen.VentasConIgv - resumen.VentasSinIgv;

        var totalProductos = ingresosPorProducto.Values.Sum(item => item.Total);
        decimal acumuladoProductos = 0;
        resumen.Productos = ingresosPorProducto
            .OrderByDescending(item => item.Value.Total)
            .ThenBy(item => item.Value.Nombre)
            .Take(10)
            .Select(item =>
            {
                acumuladoProductos += item.Value.Total;
                return new AnalyticsProducto(item.Key, item.Value.Nombre, item.Value.Unidades, item.Value.Total,
                    totalProductos == 0 ? 0 : decimal.Round(acumuladoProductos / totalProductos * 100m, 1));
            }).ToList();

        var totalClientes = resumen.VentasConIgv;
        var hoyPeru = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(HorarioPeru).DateTime);
        var idsClientes = confirmadas
            .Select(venta => venta.IdCliente)
            .Distinct()
            .ToArray();
        var historialPorCliente = new Dictionary<int, List<DateOnly>>();
        if (idsClientes.Length > 0)
        {
            var historial = await context.Venta.AsNoTracking()
                .Where(venta => venta.Estado == EstadoVenta.Confirmada
                    && idsClientes.Contains(venta.IdCliente))
                .Select(venta => new { venta.IdCliente, venta.FechaVentaUtc })
                .ToListAsync();
            foreach (var grupo in historial.GroupBy(item => item.IdCliente))
            {
                historialPorCliente[grupo.Key] = grupo
                    .Select(item => DateOnly.FromDateTime(item.FechaVentaUtc.ToOffset(HorarioPeru).DateTime))
                    .OrderBy(fecha => fecha)
                    .ToList();
            }
        }

        var gruposCliente = confirmadas
            .GroupBy(venta => new { venta.IdCliente, venta.Cliente })
            .Select(grupo => new
            {
                grupo.Key.IdCliente,
                grupo.Key.Cliente,
                Ventas = grupo.Count(),
                Total = grupo.Sum(venta => venta.Total),
                Recurrente = grupo.Count() >= 2
            })
            .ToList();
        var idsRecurrentes = gruposCliente
            .Where(grupo => grupo.Recurrente)
            .Select(grupo => grupo.IdCliente)
            .ToHashSet();
        resumen.ClientesConVentas = gruposCliente.Count;
        resumen.ClientesRecurrentes = gruposCliente.Count(grupo => grupo.Recurrente);
        var ventasRecurrentes = confirmadas
            .Where(venta => idsRecurrentes.Contains(venta.IdCliente))
            .ToList();
        var ventasNuevas = confirmadas
            .Where(venta => !idsRecurrentes.Contains(venta.IdCliente))
            .ToList();
        resumen.TicketMedioRecurrente = ventasRecurrentes.Count == 0
            ? null
            : decimal.Round(ventasRecurrentes.Sum(venta => venta.Total) / ventasRecurrentes.Count, 2);
        resumen.TicketMedioNuevo = ventasNuevas.Count == 0
            ? null
            : decimal.Round(ventasNuevas.Sum(venta => venta.Total) / ventasNuevas.Count, 2);

        decimal acumuladoClientes = 0;
        resumen.Clientes = gruposCliente
            .OrderByDescending(grupo => grupo.Total)
            .ThenBy(grupo => grupo.Cliente)
            .Take(10)
            .Select(grupo =>
            {
                acumuladoClientes += grupo.Total;
                var proporcionAcumulada = totalClientes == 0 ? 0m : acumuladoClientes / totalClientes;
                var segmento = totalClientes == 0 ? "—"
                    : proporcionAcumulada <= .8m ? "A"
                    : proporcionAcumulada <= .95m ? "B" : "C";
                List<DateOnly>? historialCliente = historialPorCliente.TryGetValue(grupo.IdCliente, out var fechas)
                    ? fechas
                    : null;
                var ultimaCompra = historialCliente is { Count: > 0 } ? historialCliente[^1] : (DateOnly?)null;
                var diasDesdeUltima = ultimaCompra is { } ultima ? Math.Max(0, hoyPeru.DayNumber - ultima.DayNumber) : (int?)null;
                var frecuenciaMedia = historialCliente is { Count: >= 2 }
                    ? decimal.Round((decimal)(historialCliente[^1].DayNumber - historialCliente[0].DayNumber) / (historialCliente.Count - 1), 1)
                    : (decimal?)null;
                return new AnalyticsCliente(grupo.IdCliente, grupo.Cliente ?? "Cliente no disponible",
                    grupo.Ventas, grupo.Total, decimal.Round(grupo.Total / grupo.Ventas, 2), segmento,
                    grupo.Recurrente, ultimaCompra, diasDesdeUltima, frecuenciaMedia);
            }).ToList();

        var dias = hasta.DayNumber - desde.DayNumber + 1;
        var agruparMes = dias > 90;
        var puntos = confirmadas.GroupBy(venta =>
        {
            var fecha = DateOnly.FromDateTime(venta.FechaVentaUtc.ToOffset(HorarioPeru).DateTime);
            return agruparMes
                ? new DateOnly(fecha.Year, fecha.Month, 1)
                : fecha.AddDays(-((int)fecha.DayOfWeek + 6) % 7);
        }).ToDictionary(grupo => grupo.Key, grupo => (Total: grupo.Sum(venta => venta.Total), Cantidad: grupo.Count()));

        var primerPeriodo = agruparMes
            ? new DateOnly(desde.Year, desde.Month, 1)
            : desde.AddDays(-((int)desde.DayOfWeek + 6) % 7);
        for (var periodo = primerPeriodo; periodo <= hasta; periodo = agruparMes ? periodo.AddMonths(1) : periodo.AddDays(7))
        {
            puntos.TryGetValue(periodo, out var valor);
            resumen.Tendencia.Add(new AnalyticsTendencia(
                agruparMes ? periodo.ToString("MM/yyyy") : $"Sem. {periodo:dd/MM}",
                valor.Total, valor.Cantidad));
        }

        var cotizaciones = await context.Cotizacion.AsNoTracking()
            .Where(cotizacion => cotizacion.FechaCreacion >= desde && cotizacion.FechaCreacion <= hasta)
            .Select(cotizacion => new { cotizacion.Estado, cotizacion.FechaVencimiento })
            .ToListAsync();
        resumen.CotizacionesEmitidas = cotizaciones.Count;
        resumen.CotizacionesConvertidas = cotizaciones.Count(cotizacion => cotizacion.Estado == "Convertida");
        resumen.CotizacionesVencidas = cotizaciones.Count(cotizacion => cotizacion.Estado == "Vencida"
            || cotizacion.Estado == "Emitida" && cotizacion.FechaVencimiento is { } limite && limite < hoyPeru);
        resumen.CotizacionesVigentes = cotizaciones.Count(cotizacion => cotizacion.Estado == "Emitida"
            && (cotizacion.FechaVencimiento is null || cotizacion.FechaVencimiento >= hoyPeru));

        var estados = await context.Pedido.AsNoTracking()
            .GroupBy(pedido => pedido.EstadoPedido)
            .Select(grupo => new { Estado = grupo.Key, Cantidad = grupo.Count() })
            .ToListAsync();
        resumen.EnCola = estados.Where(item => item.Estado == EstadoPedido.EnCola).Sum(item => item.Cantidad);
        resumen.EnProduccion = estados.Where(item => item.Estado == EstadoPedido.EnProduccion).Sum(item => item.Cantidad);
        resumen.ListosParaRecojo = estados.Where(item => item.Estado == EstadoPedido.ListoParaRecojo).Sum(item => item.Cantidad);
        resumen.Entregados = estados.Where(item => item.Estado == EstadoPedido.Entregado).Sum(item => item.Cantidad);
        resumen.PedidosCancelados = estados.Where(item => item.Estado == EstadoPedido.Cancelado).Sum(item => item.Cantidad);

        var activeWarehouses = await context.Almacenes.AsNoTracking()
            .Where(item => item.Activo && item.FechaActivacionUtc != null)
            .Select(item => new { item.IdAlmacen, item.Nombre }).ToListAsync();
        var activeWarehouseIds = activeWarehouses.Select(item => item.IdAlmacen).ToArray();
        resumen.AlertasStock = activeWarehouses.Count > 0
            ? await context.SaldosAlmacen.AsNoTracking()
                .Where(item => activeWarehouseIds.Contains(item.IdAlmacen)
                    && !item.Producto.EsServicio && item.CantidadBase <= umbralStock)
                .OrderBy(item => item.CantidadBase).ThenBy(item => item.Producto.Nombre)
                .Select(item => new AnalyticsStock(item.IdProducto, item.Producto.Nombre ?? "Artículo sin nombre",
                    item.CantidadBase, item.Almacen.Nombre))
                .Take(10).ToListAsync()
            : await context.Producto.AsNoTracking()
                .Where(producto => !producto.EsServicio && (producto.Stock ?? 0) <= umbralStock)
                .OrderBy(producto => producto.Stock).ThenBy(producto => producto.Nombre)
                .Select(producto => new AnalyticsStock(producto.IdProducto, producto.Nombre ?? "Artículo sin nombre", producto.Stock ?? 0, null))
                .Take(10).ToListAsync();

        return resumen;
    }
}

public sealed class AnalyticsWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsWarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).DateTime);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
            var timer = Stopwatch.StartNew();
            await analytics.ObtenerAsync(new DateOnly(today.Year, today.Month, 1), today, 5);
            timer.Stop();
            logger.LogInformation("Analytics dashboard queries warmed in {ElapsedMilliseconds} ms.", timer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Do not prevent normal application startup if BI warm-up fails; the next request retries it.
            logger.LogWarning(exception, "Analytics warm-up failed; dashboard will run its queries on first use.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
