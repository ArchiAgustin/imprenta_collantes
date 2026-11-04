using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal.Models;

[Table("Producto")]
public partial class Producto
{
    [Key]
    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("nombre")]
    [StringLength(100)]
    public string? Nombre { get; set; }

    [Column("descripcion")]
    [StringLength(255)]
    public string? Descripcion { get; set; }

    [Column("precio", TypeName = "decimal(10, 2)")]
    public decimal? Precio { get; set; }

    [Column("costoUnitarioSinIgv", TypeName = "decimal(10, 2)")]
    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El costo debe estar entre S/ 0 y S/ 99 999 999,99.")]
    public decimal? CostoUnitarioSinIgv { get; set; }

    [Column("stock")]
    public int? Stock { get; set; }

    // Assigned only after reviewing the real base unit of this product.
    [Column("idUnidadMedidaBase")]
    public int? IdUnidadMedidaBase { get; set; }

    [ForeignKey(nameof(IdUnidadMedidaBase))]
    public UnidadMedida? UnidadMedidaBase { get; set; }

    [Column("esServicio")]
    [Display(Name = "Es un servicio")]
    public bool EsServicio { get; set; }

    [Timestamp]
    [Column("rowVersion")]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [Column("idProveedor")]
    public int? IdProveedor { get; set; }

    [InverseProperty("IdProductoNavigation")]
    public virtual ICollection<DetalleCotizacion> DetalleCotizacions { get; set; } = new List<DetalleCotizacion>();

    [InverseProperty("IdProductoNavigation")]
    public virtual ICollection<DetallePedido> DetallePedidos { get; set; } = new List<DetallePedido>();

    public virtual ICollection<DetalleVenta> DetalleVentas { get; set; } = new List<DetalleVenta>();

    [ForeignKey("IdProveedor")]
    [InverseProperty("Productos")]
    public virtual Proveedor? IdProveedorNavigation { get; set; }
}
