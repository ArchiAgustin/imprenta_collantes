using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("CorteInventario")]
public sealed class CorteInventario
{
    [Key]
    [Column("idCorteInventario")]
    public int IdCorteInventario { get; set; }

    [Column("idAlmacen")]
    public int IdAlmacen { get; set; }

    [Column("fechaUtc")]
    public DateTimeOffset FechaUtc { get; set; }

    [Column("fechaAprobacionUtc")]
    public DateTimeOffset? FechaAprobacionUtc { get; set; }

    [Required, StringLength(20)]
    [Column("estado")]
    public string Estado { get; set; } = "Aprobado";

    [StringLength(450)]
    [Column("idUsuario")]
    public string? IdUsuario { get; set; }

    [StringLength(500)]
    [Column("observaciones")]
    public string? Observaciones { get; set; }

    [ForeignKey(nameof(IdAlmacen))]
    public Almacen Almacen { get; set; } = null!;

    public ICollection<CorteInventarioDetalle> Detalles { get; set; } = new List<CorteInventarioDetalle>();
}
