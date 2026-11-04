using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

// Imported reference data from bmotors.mae_unidadmedida. The source 'factor'
// is not a product conversion: that belongs to PresentacionProducto.
[Table("UnidadMedida")]
public sealed class UnidadMedida
{
    [Key]
    [Column("idUnidadMedida")]
    public int IdUnidadMedida { get; set; }

    [Required, StringLength(100)]
    [Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [StringLength(100)]
    [Column("descripcionCorta")]
    public string? DescripcionCorta { get; set; }

    [StringLength(100)]
    [Column("codigoSunat")]
    public string? CodigoSunat { get; set; }

    [Column("activo")]
    public bool Activo { get; set; }
}
