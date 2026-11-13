using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("TransferenciaAlmacen")]
public sealed class TransferenciaAlmacen
{
    [Key, Column("idTransferenciaAlmacen")]
    public int IdTransferenciaAlmacen { get; set; }

    [Column("idAlmacenOrigen")]
    public int IdAlmacenOrigen { get; set; }

    [Column("idAlmacenDestino")]
    public int IdAlmacenDestino { get; set; }

    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("cantidadBase", TypeName = "decimal(18,3)")]
    public decimal CantidadBase { get; set; }

    [Column("fechaUtc")]
    public DateTimeOffset FechaUtc { get; set; }

    [Required, StringLength(500), Column("motivo")]
    public string Motivo { get; set; } = string.Empty;

    [StringLength(450), Column("idUsuario")]
    public string? IdUsuario { get; set; }

    [ForeignKey(nameof(IdAlmacenOrigen))]
    public Almacen AlmacenOrigen { get; set; } = null!;

    [ForeignKey(nameof(IdAlmacenDestino))]
    public Almacen AlmacenDestino { get; set; } = null!;

    [ForeignKey(nameof(IdProducto))]
    public Producto Producto { get; set; } = null!;
}
