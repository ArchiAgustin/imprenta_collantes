using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("TramoDescuentoProducto")]
public sealed class TramoDescuentoProducto
{
    [Key]
    public int Id { get; set; }
    public int IdProducto { get; set; }
    [Range(1, 1_000_000)]
    public int CantidadMinima { get; set; }
    [Range(typeof(decimal), "0", "99.99")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal DescuentoPorcentaje { get; set; }
    public Producto Producto { get; set; } = null!;
}
