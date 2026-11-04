using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

// Will be written together with Stock and sale transactions after inventory cutover.
[Table("Kardex")]
public sealed class Kardex
{
    [Key]
    [Column("idKardex")]
    public long IdKardex { get; set; }

    [Column("idAlmacen")]
    public int IdAlmacen { get; set; }

    [Column("idProducto")]
    public int IdProducto { get; set; }

    [Column("fechaUtc")]
    public DateTimeOffset FechaUtc { get; set; }

    [Required, StringLength(30)]
    [Column("tipoMovimiento")]
    public string TipoMovimiento { get; set; } = string.Empty;

    [Column("cantidadBase", TypeName = "decimal(18,3)")]
    public decimal CantidadBase { get; set; }

    [Column("saldoAnteriorBase", TypeName = "decimal(18,3)")]
    public decimal SaldoAnteriorBase { get; set; }

    [Column("saldoPosteriorBase", TypeName = "decimal(18,3)")]
    public decimal SaldoPosteriorBase { get; set; }

    [StringLength(40)]
    [Column("tipoReferencia")]
    public string? TipoReferencia { get; set; }

    [Column("idReferencia")]
    public int? IdReferencia { get; set; }

    [StringLength(450)]
    [Column("idUsuario")]
    public string? IdUsuario { get; set; }

    [StringLength(500)]
    [Column("observaciones")]
    public string? Observaciones { get; set; }

    [ForeignKey(nameof(IdAlmacen))]
    public Almacen Almacen { get; set; } = null!;

    [ForeignKey(nameof(IdProducto))]
    public Producto Producto { get; set; } = null!;
}
