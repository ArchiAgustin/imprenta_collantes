using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class AnalyticsService(ApplicationDbContext context)
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
                linea.CostoUnitarioSinIgv
            })
            .ToListAsync();

        var ingresosPorProducto = new Dictionary<int, (string Nombre, int Unidades, decimal Total)>();
        foreach (var venta in confirmadas)
        {
            var detalle = lineas.Where(linea => linea.IdVenta == venta.IdVenta)
                .OrderBy(linea => linea.IdDetalleVenta).ToList();
            var subtotalVenta = detalle.Sum(linea => linea.Cantidad * linea.PrecioUnitario);
            resumen.VentasSinIgv += subtotalVenta;
            var igvPendiente = venta.Total - subtotalVenta;

            for (var index = 0; index < detalle.Count; index++)
            {
                var linea = detalle[index];
                var neto = linea.Cantidad * linea.PrecioUnitario;
                var igvLinea = index == detalle.Count - 1
                    ? igvPendiente
                    : subtotalVenta == 0 ? 0 : decimal.Round((venta.Total - subtotalVenta) * neto / subtotalVenta, 2, MidpointRounding.AwayFromZero);
                igvPendiente -= igvLinea;
                resumen.TotalLineas++;
                if (linea.CostoUnitarioSinIgv is { } costo)
                {
                    resumen.CostoConocidoSinIgv += linea.Cantidad * costo;
                    resumen.IngresoNetoConCostoConocido += neto;
                }
                else resumen.LineasSinCosto++;

                ingresosPorProducto.TryGetValue(linea.IdProducto, out var acumulado);
                ingresosPorProducto[linea.IdProducto] = (
                    linea.ProductoNombre,
                    acumulado.Unidades + linea.Cantidad,
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
        decimal acumuladoClientes = 0;
        resumen.Clientes = confirmadas.GroupBy(venta => new { venta.IdCliente, venta.Cliente })
            .Select(grupo => new { grupo.Key.IdCliente, grupo.Key.Cliente, Ventas = grupo.Count(), Total = grupo.Sum(venta => venta.Total) })
            .OrderByDescending(grupo => grupo.Total)
            .ThenBy(grupo => grupo.Cliente)
            .Take(10)
            .Select(grupo =>
            {
                acumuladoClientes += grupo.Total;
                var segmento = totalClientes == 0 ? "—"
                    : (acumuladoClientes - grupo.Total) / totalClientes < .8m ? "A"
                    : (acumuladoClientes - grupo.Total) / totalClientes < .95m ? "B" : "C";
                return new AnalyticsCliente(grupo.IdCliente, grupo.Cliente ?? "Cliente no disponible",
                    grupo.Ventas, grupo.Total, decimal.Round(grupo.Total / grupo.Ventas, 2), segmento);
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
        var hoyPeru = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(HorarioPeru).DateTime);
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

        var principal = await context.Almacenes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Nombre == InventarioService.NombreAlmacen);
        resumen.AlertasStock = principal?.FechaActivacionUtc is not null
            ? await context.SaldosAlmacen.AsNoTracking()
                .Where(item => item.IdAlmacen == principal.IdAlmacen && !item.Producto.EsServicio && item.CantidadBase <= umbralStock)
                .OrderBy(item => item.CantidadBase).ThenBy(item => item.Producto.Nombre)
                .Select(item => new AnalyticsStock(item.IdProducto, item.Producto.Nombre ?? "Artículo sin nombre", item.CantidadBase))
                .Take(10).ToListAsync()
            : await context.Producto.AsNoTracking()
                .Where(producto => !producto.EsServicio && (producto.Stock ?? 0) <= umbralStock)
                .OrderBy(producto => producto.Stock).ThenBy(producto => producto.Nombre)
                .Select(producto => new AnalyticsStock(producto.IdProducto, producto.Nombre ?? "Artículo sin nombre", producto.Stock ?? 0))
                .Take(10).ToListAsync();

        return resumen;
    }
}
