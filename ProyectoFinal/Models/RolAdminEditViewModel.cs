using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class RolAdminEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa el nombre del rol.")]
    [StringLength(256, ErrorMessage = "El nombre no puede superar los 256 caracteres.")]
    [Display(Name = "Nombre del rol")]
    public string Nombre { get; set; } = string.Empty;
}
