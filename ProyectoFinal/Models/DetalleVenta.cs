using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("DetalleVenta")]
public class DetalleVenta
{
    [Key]
    [Column("idDetalleVenta")]
    public int IdDetalleVenta { get; set; }

    [Column("idVenta")]
    public int IdVenta { get; set; }

    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("productoNombre")]
    [StringLength(100)]
    public string ProductoNombre { get; set; } = string.Empty;

    [Column("cantidad")]
    public int Cantidad { get; set; }

    [Column("precioUnitario", TypeName = "decimal(10, 2)")]
    public decimal PrecioUnitario { get; set; }

    [Column("costoUnitarioSinIgv", TypeName = "decimal(10, 2)")]
    public decimal? CostoUnitarioSinIgv { get; set; }

    [Column("esServicio")]
    public bool EsServicio { get; set; }

    [Column("idPresentacionProducto")]
    public int? IdPresentacionProducto { get; set; }

    [Column("factorPresentacion", TypeName = "decimal(12,3)")]
    public decimal FactorPresentacion { get; set; } = 1m;

    [Column("presentacionDescripcion"), StringLength(100)]
    public string? PresentacionDescripcion { get; set; }

    [Column("unidadBaseDescripcion"), StringLength(100)]
    public string? UnidadBaseDescripcion { get; set; }

    [ForeignKey(nameof(IdVenta))]
    public virtual Venta? Venta { get; set; }

    [ForeignKey(nameof(IdProducto))]
    public virtual Producto? Producto { get; set; }

    [ForeignKey(nameof(IdPresentacionProducto))]
    public PresentacionProducto? Presentacion { get; set; }
}
