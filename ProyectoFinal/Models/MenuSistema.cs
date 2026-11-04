using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("MenuSistema")]
public class MenuSistema
{
    [Key]
    [Column("idMenu")]
    public int IdMenu { get; set; }

    [Column("codigo")]
    [StringLength(64)]
    public string Codigo { get; set; } = string.Empty;

    [Column("nombre")]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Column("controllerNombre")]
    [StringLength(100)]
    public string ControllerNombre { get; set; } = string.Empty;

    [Column("orden")]
    public int Orden { get; set; }

    [Column("activo")]
    public bool Activo { get; set; } = true;

    public virtual ICollection<PermisoRolMenu> Permisos { get; set; } = new List<PermisoRolMenu>();
}
