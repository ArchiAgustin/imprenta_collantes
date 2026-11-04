using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ProyectoFinal.Models;

[Table("PermisoRolMenu")]
public class PermisoRolMenu
{
    [Column("idPermiso")]
    public int IdPermiso { get; set; }

    [Column("idMenu")]
    public int MenuSistemaId { get; set; }

    [Column("idRol")]
    public string RoleId { get; set; } = string.Empty;

    [Column("ver")]
    public bool PuedeVer { get; set; }

    [Column("crear")]
    public bool PuedeCrear { get; set; }

    [Column("editar")]
    public bool PuedeEditar { get; set; }

    [Column("eliminar")]
    public bool PuedeEliminar { get; set; }

    [Column("procesar")]
    public bool PuedeProcesar { get; set; }

    [ForeignKey(nameof(MenuSistemaId))]
    public virtual MenuSistema? Menu { get; set; }

    [ForeignKey(nameof(RoleId))]
    public virtual IdentityRole? Rol { get; set; }
}
