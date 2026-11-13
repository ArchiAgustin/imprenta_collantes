using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class FichaComercialService(ApplicationDbContext db)
{
    private const int FilasPorPagina = 10;

    public async Task<ClienteFichaComercialViewModel?> ObtenerAsync(int clienteId, int paginaCotizaciones, int paginaPedidos)
    {
        var cliente = await db.Cliente.AsNoTracking()
            .Include(item => item.DocumentoIdentidadTipo)
            .Include(item => item.Ubigeo)
            .SingleOrDefaultAsync(item => item.IdCliente == clienteId);
        if (cliente is null) return null;

        var ventas = db.Venta.AsNoTracking()
            .Where(item => item.IdCliente == clienteId && item.Estado == EstadoVenta.Confirmada);
        var ventasConfirmadas = await ventas.CountAsync();
        var totalConfirmado = await ventas.Select(item => (decimal?)item.Total).SumAsync() ?? 0m;

        var cotizaciones = db.Cotizacion.AsNoTracking().Where(item => item.IdCliente == clienteId);
        var totalCotizaciones = await cotizaciones.CountAsync();
        var paginasCotizaciones = Paginas(totalCotizaciones);
        paginaCotizaciones = NormalizarPagina(paginaCotizaciones, paginasCotizaciones);
        var cotizacionRows = await cotizaciones
            .OrderByDescending(item => item.FechaCreacion).ThenByDescending(item => item.IdCotizacion)
            .Skip((paginaCotizaciones - 1) * FilasPorPagina).Take(FilasPorPagina)
            .Select(item => new
            {
                item.IdCotizacion,
                item.SerieInterna,
                item.CorrelativoInterno,
                item.DigitosNumeroInterno,
                item.FechaCreacion,
                item.FechaVencimiento,
                item.Estado,
                item.Total
            }).ToListAsync();
        var cotizacionItems = cotizacionRows.Select(item => new CotizacionFichaItem(
            item.IdCotizacion,
            NumeroInterno(item.SerieInterna, item.CorrelativoInterno, item.DigitosNumeroInterno, item.IdCotizacion),
            item.FechaCreacion, item.FechaVencimiento, item.Estado, item.Total)).ToList();

        var pedidos = db.Pedido.AsNoTracking().Where(item => item.IdCliente == clienteId);
        var totalPedidos = await pedidos.CountAsync();
        var paginasPedidos = Paginas(totalPedidos);
        paginaPedidos = NormalizarPagina(paginaPedidos, paginasPedidos);
        var pedidoRows = await pedidos
            .OrderByDescending(item => item.FechaPedido).ThenByDescending(item => item.IdPedido)
            .Skip((paginaPedidos - 1) * FilasPorPagina).Take(FilasPorPagina)
            .Select(item => new
            {
                item.IdPedido,
                item.IdVenta,
                SerieVenta = item.IdVentaNavigation == null ? null : item.IdVentaNavigation.SerieInterna,
                CorrelativoVenta = item.IdVentaNavigation == null ? null : item.IdVentaNavigation.CorrelativoInterno,
                DigitosVenta = item.IdVentaNavigation == null ? null : item.IdVentaNavigation.DigitosNumeroInterno,
                item.FechaPedido,
                item.EstadoPedido,
                item.Total
            }).ToListAsync();
        var pedidoItems = pedidoRows.Select(item => new PedidoFichaItem(
            item.IdPedido,
            item.IdVenta,
            item.IdVenta is null ? null : NumeroInterno(item.SerieVenta, item.CorrelativoVenta, item.DigitosVenta, item.IdVenta.Value),
            item.FechaPedido,
            item.EstadoPedido ?? "Sin estado",
            item.Total)).ToList();

        return new ClienteFichaComercialViewModel
        {
            Cliente = cliente,
            VentasConfirmadas = ventasConfirmadas,
            TotalVentasConfirmadasConIgv = totalConfirmado,
            TotalCotizaciones = totalCotizaciones,
            Cotizaciones = cotizacionItems,
            PaginaCotizaciones = paginaCotizaciones,
            PaginasCotizaciones = paginasCotizaciones,
            TotalPedidos = totalPedidos,
            Pedidos = pedidoItems,
            PaginaPedidos = paginaPedidos,
            PaginasPedidos = paginasPedidos
        };
    }

    private static int Paginas(int total) => Math.Max(1, (int)Math.Ceiling(total / (double)FilasPorPagina));

    private static int NormalizarPagina(int pagina, int paginas) => Math.Clamp(pagina, 1, paginas);

    private static string NumeroInterno(string? serie, int? correlativo, int? digitos, int id) =>
        serie is { Length: > 0 } && correlativo is { } numero
            ? $"{serie}-{numero.ToString("D" + (digitos is >= 4 and <= 10 ? digitos.Value : 6))}"
            : $"Histórica #{id}";
}
