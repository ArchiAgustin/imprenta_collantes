namespace ProyectoFinal.Models;

public sealed class ClienteFichaComercialViewModel
{
    public Cliente Cliente { get; init; } = null!;
    public int VentasConfirmadas { get; init; }
    public decimal TotalVentasConfirmadasConIgv { get; init; }
    public int TotalCotizaciones { get; init; }
    public IReadOnlyList<CotizacionFichaItem> Cotizaciones { get; init; } = Array.Empty<CotizacionFichaItem>();
    public int PaginaCotizaciones { get; init; }
    public int PaginasCotizaciones { get; init; }
    public int TotalPedidos { get; init; }
    public IReadOnlyList<PedidoFichaItem> Pedidos { get; init; } = Array.Empty<PedidoFichaItem>();
    public int PaginaPedidos { get; init; }
    public int PaginasPedidos { get; init; }
}

public sealed record CotizacionFichaItem(
    int IdCotizacion,
    string Numero,
    DateOnly? FechaEmision,
    DateOnly? FechaVencimiento,
    string Estado,
    decimal? Total);

public sealed record PedidoFichaItem(
    int IdPedido,
    int? IdVenta,
    string? NumeroVenta,
    DateOnly? FechaPedido,
    string Estado,
    decimal? Total);
