using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ReferenciaPrecio(
    int IdProducto,
    string NombreProducto,
    int CantidadSolicitada,
    decimal? PrecioCatalogoSinIgv,
    decimal? PrecioMinimoSinIgv,
    decimal? DescuentoMaximoPorcentaje,
    decimal? UltimoPrecioClienteSinIgv,
    int? UltimaCantidadCliente,
    decimal? PrecioHistoricoAplicableSinIgv,
    bool CostoEstimado);

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
                Fecha = linea.Venta!.FechaVentaUtc
            }).ToListAsync();

        var resultado = new List<ReferenciaPrecio>(lineas.Count);
        foreach (var linea in lineas)
        {
            if (linea.ProductoId is not { } id || !productos.TryGetValue(id, out var producto)) continue;
            var precioCatalogo = producto.Precio is > 0m ? producto.Precio : null;
            var minimo = CalcularMinimo(producto.CostoUnitarioSinIgv);
            decimal? maximo = null;
            if (minimo is { } piso && precioCatalogo is { } precio && precio >= piso)
                maximo = decimal.Floor((precio - piso) / precio * 10_000m) / 100m;

            var ultima = anteriores.Where(item => item.IdProducto == id)
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

            resultado.Add(new ReferenciaPrecio(
                id, producto.Nombre ?? "Producto sin nombre", linea.Cantidad, precioCatalogo,
                minimo, maximo, precioAnterior, ultima?.Cantidad, referenciaAplicable, producto.EsServicio));
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
