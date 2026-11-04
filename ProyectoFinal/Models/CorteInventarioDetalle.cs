using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("CorteInventarioDetalle")]
public sealed class CorteInventarioDetalle
{
    [Key]
    [Column("idCorteInventarioDetalle")]
    public int IdCorteInventarioDetalle { get; set; }

    [Column("idCorteInventario")]
    public int IdCorteInventario { get; set; }

    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("cantidadSistemaBase", TypeName = "decimal(18,3)")]
    public decimal? CantidadSistemaBase { get; set; }

    [Column("cantidadContadaBase", TypeName = "decimal(18,3)")]
    public decimal CantidadContadaBase { get; set; }

    [Column("conteoRegistrado")]
    public bool ConteoRegistrado { get; set; }

    [ForeignKey(nameof(IdCorteInventario))]
    public CorteInventario Corte { get; set; } = null!;

    [ForeignKey(nameof(IdProducto))]
    public Producto Producto { get; set; } = null!;
}
