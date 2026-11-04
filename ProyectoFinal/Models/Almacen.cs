using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("Almacen")]
public sealed class Almacen
{
    [Key]
    [Column("idAlmacen")]
    public int IdAlmacen { get; set; }

    [Required, StringLength(150)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("conteoEnCurso")]
    public bool ConteoEnCurso { get; set; }

    // Null until the administrator approves a physical opening count.
    [Column("fechaActivacionUtc")]
    public DateTimeOffset? FechaActivacionUtc { get; set; }
}
