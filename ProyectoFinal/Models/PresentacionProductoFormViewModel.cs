using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class PresentacionProductoFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una unidad de medida.")]
    public int IdUnidadMedida { get; set; }

    [Required, StringLength(100)]
    public string Descripcion { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.001", "999999999.999", ErrorMessage = "El factor debe ser positivo y admitir hasta tres decimales.")]
    public decimal Factor { get; set; }

    [StringLength(100)]
    public string? CodigoBarras { get; set; }
}
