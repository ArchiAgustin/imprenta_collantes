using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProyectoFinal.Services;

namespace ProyectoFinal.Models;

public sealed class CotizacionFormViewModel
{
    public int? IdCotizacion { get; set; }

    public int? IdCotizacionOrigen { get; set; }

    [Required(ErrorMessage = "Selecciona un cliente.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un cliente válido.")]
    public int? ClienteId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? FechaVencimiento { get; set; }

    [StringLength(4000, ErrorMessage = "La descripción no puede superar los 4000 caracteres.")]
    public string? Descripcion { get; set; }

    public List<CotizacionLineaInputViewModel> Lineas { get; set; } = new() { new() };

    [BindNever]
    public List<Cliente> Clientes { get; set; } = new();

    [BindNever]
    public List<Producto> Productos { get; set; } = new();

    [BindNever]
    public DateOnly FechaEmision { get; set; }

    [BindNever]
    public string Usuario { get; set; } = string.Empty;

    [BindNever]
    public CotizacionTotalesViewModel Totales { get; private set; } = new();

    [BindNever]
    public IReadOnlyList<ReferenciaPrecio> ReferenciasPrecio { get; set; } = Array.Empty<ReferenciaPrecio>();

    public void RecalcularTotales()
    {
        if (Lineas.Count > 100 || Lineas.Any(linea => linea is null
            || linea.Cantidad is < 1 or > 1_000_000
            || linea.PrecioUnitario is < 0 or > 99_999_999.99m))
        {
            Totales = new CotizacionTotalesViewModel();
            return;
        }

        var subtotal = Lineas.Sum(linea => decimal.Round(linea.Cantidad * linea.PrecioUnitario, 2, MidpointRounding.AwayFromZero));
        var igv = decimal.Round(subtotal * 0.18m, 2, MidpointRounding.AwayFromZero);
        Totales = new CotizacionTotalesViewModel(subtotal, igv, subtotal + igv);
    }
}

public sealed class CotizacionLineaInputViewModel
{
    [Required(ErrorMessage = "Selecciona un producto.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un producto válido.")]
    public int? ProductoId { get; set; }

    [Range(1, 1_000_000, ErrorMessage = "La cantidad debe estar entre 1 y 1 000 000.")]
    public int Cantidad { get; set; } = 1;

    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "Ingresa un precio válido de hasta S/ 99 999 999,99.")]
    public decimal PrecioUnitario { get; set; }
}

public sealed record CotizacionTotalesViewModel(decimal Subtotal, decimal Igv, decimal Total)
{
    public CotizacionTotalesViewModel() : this(0, 0, 0) { }

    public string SubtotalMoneda => Subtotal.ToString("C2", CultureInfo.GetCultureInfo("es-PE"));
    public string IgvMoneda => Igv.ToString("C2", CultureInfo.GetCultureInfo("es-PE"));
    public string TotalMoneda => Total.ToString("C2", CultureInfo.GetCultureInfo("es-PE"));
}
