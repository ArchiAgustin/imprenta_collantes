using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("Inventario")]
public sealed class Inventario
{
    [Key, Column("idInventario")]
    public int IdInventario { get; set; }

    [Required, StringLength(8), Column("serie")]
    public string Serie { get; set; } = string.Empty;

    [Column("correlativo")]
    public int Correlativo { get; set; }

    [Column("fecha", TypeName = "date")]
    public DateOnly Fecha { get; set; }

    [Column("creadoUtc")]
    public DateTimeOffset CreadoUtc { get; set; }

    [Column("idAlmacen")]
    public int IdAlmacen { get; set; }

    [StringLength(450), Column("idUsuario")]
    public string? IdUsuario { get; set; }

    // Link to the legacy approved cut when it is imported into the new history.
    [Column("idCorteInventarioOrigen")]
    public int? IdCorteInventarioOrigen { get; set; }

    [NotMapped]
    public string Numero => $"{Serie}-{Correlativo:D8}";

    [ForeignKey(nameof(IdAlmacen))]
    public Almacen Almacen { get; set; } = null!;

    [ForeignKey(nameof(IdCorteInventarioOrigen))]
    public CorteInventario? CorteInventarioOrigen { get; set; }

    public ICollection<InventarioDetalle> Detalles { get; set; } = new List<InventarioDetalle>();
}
