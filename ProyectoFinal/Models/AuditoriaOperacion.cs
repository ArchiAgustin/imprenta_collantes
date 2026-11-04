using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("AuditoriaOperacion")]
public class AuditoriaOperacion
{
    [Key]
    [Column("idAuditoria")]
    public long IdAuditoria { get; set; }

    [Column("tipoEntidad")]
    [StringLength(40)]
    public string TipoEntidad { get; set; } = string.Empty;

    [Column("idEntidad")]
    public int IdEntidad { get; set; }

    [Column("accion")]
    [StringLength(60)]
    public string Accion { get; set; } = string.Empty;

    [Column("estadoAnterior")]
    [StringLength(30)]
    public string? EstadoAnterior { get; set; }

    [Column("estadoNuevo")]
    [StringLength(30)]
    public string? EstadoNuevo { get; set; }

    [Column("detalle")]
    [StringLength(500)]
    public string? Detalle { get; set; }

    [Column("idUsuario")]
    [StringLength(450)]
    public string? IdUsuario { get; set; }

    [Column("fechaUtc")]
    public DateTimeOffset FechaUtc { get; set; }
}
