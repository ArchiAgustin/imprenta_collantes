namespace ProyectoFinal.Models;

public sealed class AnalyticsDashboardViewModel
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public int UmbralStock { get; set; } = 5;
    public int VentasConfirmadas { get; set; }
    public int VentasCanceladas { get; set; }
    public decimal VentasConIgv { get; set; }
    public decimal VentasSinIgv { get; set; }
    public decimal Igv { get; set; }
    public decimal CostoConocidoSinIgv { get; set; }
    public decimal IngresoNetoConCostoConocido { get; set; }
    public int LineasSinCosto { get; set; }
    public int TotalLineas { get; set; }
    public decimal? UtilidadEstimada => TotalLineas == LineasSinCosto
        ? null
        : IngresoNetoConCostoConocido - CostoConocidoSinIgv;
    public decimal? MargenEstimadoPorcentaje => IngresoNetoConCostoConocido > 0 && UtilidadEstimada is { } utilidad
        ? decimal.Round(utilidad / IngresoNetoConCostoConocido * 100m, 1)
        : null;
    public int CotizacionesEmitidas { get; set; }
    public int CotizacionesConvertidas { get; set; }
    public int CotizacionesVencidas { get; set; }
    public int CotizacionesVigentes { get; set; }
    public decimal TasaConversionPorcentaje => CotizacionesEmitidas == 0
        ? 0
        : decimal.Round((decimal)CotizacionesConvertidas / CotizacionesEmitidas * 100m, 1);
    public int EnCola { get; set; }
    public int EnProduccion { get; set; }
    public int ListosParaRecojo { get; set; }
    public int Entregados { get; set; }
    public int PedidosCancelados { get; set; }
    public List<AnalyticsTendencia> Tendencia { get; set; } = new();
    public List<AnalyticsProducto> Productos { get; set; } = new();
    public List<AnalyticsCliente> Clientes { get; set; } = new();
    public List<AnalyticsStock> AlertasStock { get; set; } = new();
}

public sealed record AnalyticsTendencia(string Etiqueta, decimal TotalConIgv, int Ventas);
public sealed record AnalyticsProducto(int IdProducto, string Nombre, int Unidades, decimal TotalConIgv, decimal PorcentajeAcumulado);
public sealed record AnalyticsCliente(int IdCliente, string Nombre, int Ventas, decimal TotalConIgv, decimal TicketPromedio, string Segmento);
public sealed record AnalyticsStock(int IdProducto, string Nombre, decimal Existencias);
