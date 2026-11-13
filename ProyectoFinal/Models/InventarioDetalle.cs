using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("InventarioDetalle")]
public sealed class InventarioDetalle
{
    [Key, Column("idInventarioDetalle")]
    public int IdInventarioDetalle { get; set; }

    [Column("idInventario")]
    public int IdInventario { get; set; }

    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("cantidadSistemaBase", TypeName = "decimal(18,3)")]
    public decimal? CantidadSistemaBase { get; set; }

    [Column("cantidadContadaBase", TypeName = "decimal(18,3)")]
    public decimal CantidadContadaBase { get; set; }

    [ForeignKey(nameof(IdInventario))]
    public Inventario Inventario { get; set; } = null!;

    [ForeignKey(nameof(IdProducto))]
    public Producto Producto { get; set; } = null!;
}
