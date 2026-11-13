using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

// Operational balance per warehouse and product. Legacy Producto.Stock remains
// untouched for historical records; physical counts establish each warehouse's stock.
[Table("Stock")]
public sealed class StockAlmacen
{
    [Column("idAlmacen")]
    public int IdAlmacen { get; set; }

    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("cantidadBase", TypeName = "decimal(18,3)")]
    public decimal CantidadBase { get; set; }

    [Timestamp]
    [Column("rowVersion")]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Almacen Almacen { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
