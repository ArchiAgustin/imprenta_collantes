using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class InventarioIndexViewModel
{
    public IReadOnlyList<InventarioTablaFilaViewModel> Inventarios { get; set; } = Array.Empty<InventarioTablaFilaViewModel>();
    public IReadOnlyList<Almacen> Almacenes { get; set; } = Array.Empty<Almacen>();
    public int TotalArticulos { get; set; }
    public int ArticulosSinUnidadBase { get; set; }
    public string Serie { get; set; } = "INV1";
    public int SiguienteCorrelativo { get; set; } = 2;
    public DateOnly FechaHoy { get; set; }
}

public sealed record InventarioTablaFilaViewModel(
    int IdInventario,
    string Serie,
    int Correlativo,
    DateOnly Fecha,
    string Almacen,
    int Productos);

public sealed class NuevoInventarioViewModel
{
    [Range(1, int.MaxValue)]
    public int IdAlmacen { get; set; }

    [Required]
    public DateOnly? Fecha { get; set; }

    public List<NuevoInventarioDetalleViewModel> Detalles { get; set; } = new();
}

public sealed class NuevoInventarioDetalleViewModel
{
    public int IdProducto { get; set; }

    [Required, Range(typeof(decimal), "0", "999999999999999.999", ErrorMessage = "Ingresa una cantidad no negativa.")]
    public decimal? CantidadBase { get; set; }
}

public sealed class KardexInventarioViewModel
{
    public int? AlmacenSeleccionadoId { get; set; }
    public IReadOnlyList<Almacen> Almacenes { get; set; } = Array.Empty<Almacen>();
    public IReadOnlyList<Kardex> Movimientos { get; set; } = Array.Empty<Kardex>();
}

public sealed class StockPorAlmacenViewModel
{
    public string ProductoNombre { get; set; } = string.Empty;
    public string? UnidadBase { get; set; }
    public bool EsServicio { get; set; }
    public IReadOnlyList<StockPorAlmacenFilaViewModel> Almacenes { get; set; } = Array.Empty<StockPorAlmacenFilaViewModel>();
}

public sealed record StockPorAlmacenFilaViewModel(
    string Nombre,
    bool Activo,
    bool CorteInicialAplicado,
    decimal? CantidadBase);

public sealed class AlmacenFormViewModel
{
    public int? IdAlmacen { get; set; }

    public bool EsPredeterminado { get; set; }

    [Required, StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}

public sealed record AlmacenAdminRowViewModel(
    int Id,
    string Nombre,
    bool Activo,
    bool EsPredeterminado,
    bool CorteInicialAplicado,
    int SaldosConExistencia,
    bool TieneTrabajoPendiente);

public sealed class TransferenciaAlmacenViewModel
{
    [Range(1, int.MaxValue)]
    public int IdAlmacenOrigen { get; set; }

    [Range(1, int.MaxValue)]
    public int IdAlmacenDestino { get; set; }

    [Range(1, int.MaxValue)]
    public int IdProducto { get; set; }

    [Range(typeof(decimal), "0.001", "999999999999999.999", ErrorMessage = "Ingresa una cantidad positiva.")]
    public decimal CantidadBase { get; set; }

    [Required, StringLength(500)]
    public string Motivo { get; set; } = string.Empty;

    public IReadOnlyList<Almacen> Almacenes { get; set; } = Array.Empty<Almacen>();
    public IReadOnlyList<Producto> Productos { get; set; } = Array.Empty<Producto>();
}

public sealed record ResultadoTransferenciaInventario(bool Exito, string Mensaje, int? IdTransferencia = null);

public sealed class TransferenciasInventarioViewModel
{
    public TransferenciaAlmacenViewModel Formulario { get; set; } = new();
    public IReadOnlyList<TransferenciaAlmacen> Historial { get; set; } = Array.Empty<TransferenciaAlmacen>();
}

public sealed class ConfirmarVentaViewModel
{
    public int CotizacionId { get; set; }
    public int IdAlmacen { get; set; }
    public IReadOnlyList<Almacen> Almacenes { get; set; } = Array.Empty<Almacen>();
}
