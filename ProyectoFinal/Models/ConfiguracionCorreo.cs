using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("ConfiguracionCorreo")]
public sealed class ConfiguracionCorreo
{
    [Key, Column("id")]
    public int Id { get; set; } = 1;

    [Required, StringLength(250), Column("servidor")]
    public string Servidor { get; set; } = string.Empty;

    [Column("puerto")]
    public int Puerto { get; set; } = 587;

    [Required, EmailAddress, StringLength(254), Column("correoRemitente")]
    public string CorreoRemitente { get; set; } = string.Empty;

    [Required, StringLength(100), Column("nombreRemitente")]
    public string NombreRemitente { get; set; } = "Imprenta Collantes";

    [StringLength(250), Column("usuario")]
    public string? Usuario { get; set; }

    [StringLength(2000), Column("contrasenaProtegida")]
    public string? ContrasenaProtegida { get; set; }

    [Column("usarTls")]
    public bool UsarTls { get; set; } = true;

    [Column("actualizadoUtc")]
    public DateTimeOffset ActualizadoUtc { get; set; }
}
