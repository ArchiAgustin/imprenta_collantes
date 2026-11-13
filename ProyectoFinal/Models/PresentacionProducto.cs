using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("PresentacionProducto")]
public sealed class PresentacionProducto
{
    [Key]
    [Column("idPresentacionProducto")]
    public int IdPresentacionProducto { get; set; }

    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("idUnidadMedida")]
    public int IdUnidadMedida { get; set; }

    [Required, StringLength(100)]
    [Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    // Number of base units in one presentation; never use UnidadMedida's source factor.
    [Range(typeof(decimal), "0.001", "999999999.999", ErrorMessage = "El factor debe ser positivo y admite hasta tres decimales.")]
    [Column("factor", TypeName = "decimal(12,3)")]
    public decimal Factor { get; set; }

    [StringLength(100)]
    [Column("codigoBarras")]
    public string? CodigoBarras { get; set; }

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [ForeignKey(nameof(IdProducto))]
    public Producto Producto { get; set; } = null!;

    [ForeignKey(nameof(IdUnidadMedida))]
    public UnidadMedida UnidadMedida { get; set; } = null!;
}
