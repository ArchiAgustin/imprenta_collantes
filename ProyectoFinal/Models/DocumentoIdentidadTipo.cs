using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("documentoidentidad_tipo")]
public sealed class DocumentoIdentidadTipo
{
    [Key]
    [Column("iddocumentoidentidadtipo")]
    public int IdDocumentoIdentidadTipo { get; set; }

    [Required, StringLength(5)]
    [Column("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(20)]
    [Column("abreviatura")]
    public string? Abreviatura { get; set; }

    [Column("longitud")]
    public byte? Longitud { get; set; }

    [Column("estado")]
    public bool Estado { get; set; }

    [Column("eliminado")]
    public bool Eliminado { get; set; }

    [Column("idusuario_crea")]
    public int? IdUsuarioCrea { get; set; }

    [Column("idusuario_modifica")]
    public int? IdUsuarioModifica { get; set; }

    [Column("idusuario_elimina")]
    public int? IdUsuarioElimina { get; set; }

    [Column("fecha_crea")]
    public DateTime FechaCrea { get; set; }

    [Column("fecha_modifica")]
    public DateTime? FechaModifica { get; set; }

    [Column("fecha_elimina")]
    public DateTime? FechaElimina { get; set; }
}
