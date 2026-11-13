using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ReferenciaPrecio(
    int IdProducto,
    string NombreProducto,
    string Presentacion,
    int CantidadSolicitada,
    decimal? PrecioCatalogoSinIgv,
    decimal? PrecioMinimoSinIgv,
    decimal? DescuentoMaximoPorcentaje,
    decimal? UltimoPrecioClienteSinIgv,
    int? UltimaCantidadCliente,
    decimal? PrecioHistoricoAplicableSinIgv,
    bool CostoEstimado,
    decimal? PrecioRecomendadoSinIgv,
    decimal? DescuentoTramoPorcentaje,
    int? CantidadMinimaTramo,
    string? MotivoRecomendacion);

public sealed class SugerenciaPrecioService(ApplicationDbContext context)
{
    private const decimal MargenMinimo = 0.10m;

    public async Task<IReadOnlyList<ReferenciaPrecio>> ObtenerAsync(int clienteId, IReadOnlyList<CotizacionLineaInputViewModel> lineas)
    {
        var ids = lineas.Where(line => line.ProductoId.HasValue)
            .Select(line => line.ProductoId!.Value).Distinct().ToArray();
        var productos = await context.Producto.AsNoTracking()
            .Where(producto => ids.Contains(producto.IdProducto))
            .ToDictionaryAsync(producto => producto.IdProducto);
        var tramos = await context.TramosDescuentoProducto.AsNoTracking()
            .Where(item => ids.Contains(item.IdProducto)).ToListAsync();

        // Historic prices stay local. Only confirmed sales of the selected client and
        // product are considered; no customer data or costs are sent to Groq.
        var anteriores = await context.DetalleVenta.AsNoTracking()
            .Where(linea => ids.Contains(linea.IdProducto) && linea.Venta != null
                && linea.Venta.IdCliente == clienteId && linea.Venta.Estado == EstadoVenta.Confirmada)
            .Select(linea => new
            {
                linea.IdProducto,
                linea.IdDetalleVenta,
                linea.PrecioUnitario,
                linea.Cantidad,
                linea.FactorPresentacion,
                Fecha = linea.Venta!.FechaVentaUtc
            }).ToListAsync();

        var resultado = new List<ReferenciaPrecio>(lineas.Count);
        foreach (var linea in lineas)
        {
            if (linea.ProductoId is not { } id || !productos.TryGetValue(id, out var producto)) continue;
            var factor = linea.FactorPresentacion > 0m ? linea.FactorPresentacion : 1m;
            var precioCatalogo = producto.Precio is > 0m ? producto.Precio * factor : null;
            var minimo = CalcularMinimo(producto.CostoUnitarioSinIgv is { } cost ? cost * factor : null);
            decimal? maximo = null;
            if (minimo is { } piso && precioCatalogo is { } precio && precio >= piso)
                maximo = decimal.Floor((precio - piso) / precio * 10_000m) / 100m;

            var ultima = anteriores.Where(item => item.IdProducto == id && item.FactorPresentacion == factor)
                .OrderByDescending(item => item.Fecha)
                .ThenByDescending(item => item.IdDetalleVenta)
                .FirstOrDefault();
            decimal? precioAnterior = ultima?.PrecioUnitario;
            decimal? referenciaAplicable = null;
            if (minimo is { } limite && precioCatalogo is { } catalogo
                && ultima is { Cantidad: > 0, PrecioUnitario: > 0m }
                && linea.Cantidad >= ultima.Cantidad && ultima.PrecioUnitario >= limite
                && ultima.PrecioUnitario < catalogo)
                referenciaAplicable = ultima.PrecioUnitario;

            var tramo = tramos.Where(item => item.IdProducto == id && item.CantidadMinima <= linea.Cantidad)
                .OrderByDescending(item => item.CantidadMinima).FirstOrDefault();
            decimal? recomendado = null;
            string? motivo = null;
            if (tramo is not null && minimo is { } pisoSeguro && precioCatalogo is { } basePrecio && basePrecio >= pisoSeguro)
            {
                var precioPorTramo = decimal.Ceiling(basePrecio * (1m - tramo.DescuentoPorcentaje / 100m) * 100m) / 100m;
                recomendado = Math.Max(precioPorTramo, pisoSeguro);
                if (referenciaAplicable is { } precioCliente)
                    recomendado = Math.Max(recomendado.Value, precioCliente);
                motivo = $"Tramo desde {tramo.CantidadMinima} unidades: {tramo.DescuentoPorcentaje:0.##} %; "
                    + (referenciaAplicable is null ? "sin historial comparable; " : "se consideró la última venta comparable; ")
                    + (recomendado == pisoSeguro ? "limitado por el margen mínimo del 10 %." : "respeta el margen mínimo del 10 %.");
            }

            resultado.Add(new ReferenciaPrecio(
                id, producto.Nombre ?? "Producto sin nombre", linea.PresentacionDescripcion ?? "Unidad base",
                linea.Cantidad, precioCatalogo,
                minimo, maximo, precioAnterior, ultima?.Cantidad, referenciaAplicable, producto.EsServicio,
                recomendado, tramo?.DescuentoPorcentaje, tramo?.CantidadMinima, motivo));
        }
        return resultado;
    }

    public static decimal? CalcularMinimo(decimal? costoUnitarioSinIgv)
    {
        // Zero or missing cost cannot establish a trustworthy gross-margin floor.
        if (costoUnitarioSinIgv is null or <= 0m) return null;
        // Ceiling (not standard rounding) is essential: 111.11 on a cost of 100
        // yields slightly LESS than a 10% margin, while 111.12 preserves it.
        return decimal.Ceiling(costoUnitarioSinIgv.Value / (1m - MargenMinimo) * 100m) / 100m;
    }
}
