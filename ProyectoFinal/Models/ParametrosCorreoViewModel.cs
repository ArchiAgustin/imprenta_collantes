using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class ParametrosCorreoViewModel
{
    [Required, StringLength(250, MinimumLength = 1)]
    [Display(Name = "Servidor SMTP")]
    public string Servidor { get; set; } = string.Empty;

    [Range(1, 65535)]
    [Display(Name = "Puerto")]
    public int Puerto { get; set; } = 587;

    [Required, EmailAddress, StringLength(254)]
    [Display(Name = "Correo remitente")]
    public string CorreoRemitente { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Nombre remitente")]
    public string NombreRemitente { get; set; } = "Imprenta Collantes";

    [StringLength(250)]
    [Display(Name = "Usuario SMTP")]
    public string? Usuario { get; set; }

    [DataType(DataType.Password), StringLength(256)]
    [Display(Name = "Contraseña SMTP")]
    public string? Contrasena { get; set; }

    [Display(Name = "Usar TLS (recomendado)")]
    public bool UsarTls { get; set; } = true;

    public bool TieneContrasenaGuardada { get; set; }
    public string? UsuarioPrueba { get; set; }
}
