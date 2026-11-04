using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class PermisosRolViewModel
{
    [Required]
    public string RolId { get; set; } = string.Empty;

    public string RolNombre { get; set; } = string.Empty;

    public List<MenuPermisoItemViewModel> Menus { get; set; } = new();
}

public sealed class MenuPermisoItemViewModel
{
    public int MenuId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string ControllerNombre { get; set; } = string.Empty;
    public bool PuedeVer { get; set; }
    public bool PuedeCrear { get; set; }
    public bool PuedeEditar { get; set; }
    public bool PuedeEliminar { get; set; }
    public bool PuedeProcesar { get; set; }
}
