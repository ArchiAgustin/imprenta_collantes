using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class InventarioDashboardViewModel
{
    public bool Activo { get; set; }
    public bool ConteoEnCurso { get; set; }
    public DateTimeOffset? FechaActivacionUtc { get; set; }
    public CorteInventario? CorteAbierto { get; set; }
    public List<InventarioSaldoViewModel> Saldos { get; set; } = new();
    public List<Kardex> Movimientos { get; set; } = new();
    public List<CorteInventario> Cortes { get; set; } = new();
}

public sealed record InventarioSaldoViewModel(int IdProducto, string Nombre, decimal CantidadBase);

public sealed class ConteoViewModel
{
    public int IdCorte { get; set; }
    public bool Apertura { get; set; }
    public DateTimeOffset FechaUtc { get; set; }
    public List<ConteoArticuloViewModel> Articulos { get; set; } = new();
}

public sealed class ConteoArticuloViewModel
{
    public int IdProducto { get; set; }
    public string? Nombre { get; set; }
    public decimal? CantidadSistemaBase { get; set; }

    [Range(typeof(decimal), "0", "999999999999999.999", ErrorMessage = "Ingresa una cantidad no negativa.")]
    public decimal? CantidadContadaBase { get; set; }
}

public sealed class EntradaInventarioViewModel
{
    [Range(1, int.MaxValue)]
    public int IdProducto { get; set; }

    [Range(typeof(decimal), "0.001", "999999999999999.999", ErrorMessage = "Ingresa una cantidad positiva.")]
    public decimal CantidadBase { get; set; }

    [Required, StringLength(500)]
    public string Motivo { get; set; } = string.Empty;
}
