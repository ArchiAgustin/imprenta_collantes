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

    [Column("idMenuPadre")]
    public int? IdMenuPadre { get; set; }

    [Column("icono"), StringLength(32)]
    public string? Icono { get; set; }

    // Stable key resolved only through MenuDestinationRegistry. Null means a group.
    [Column("destino"), StringLength(64)]
    public string? Destino { get; set; }

    // Kept as a read-compatible column for existing installations. New links are
    // rendered from the allow-listed destination key, never from arbitrary input.
    [Column("controllerNombre"), StringLength(100)]
    public string? ControllerNombre { get; set; }

    [Column("orden")]
    public int Orden { get; set; }

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [NotMapped]
    public int Profundidad { get; set; }

    [NotMapped]
    public string ContextoJerarquia { get; set; } = string.Empty;

    [ForeignKey(nameof(IdMenuPadre))]
    public virtual MenuSistema? MenuPadre { get; set; }

    public virtual ICollection<MenuSistema> Hijos { get; set; } = new List<MenuSistema>();

    public virtual ICollection<PermisoRolMenu> Permisos { get; set; } = new List<PermisoRolMenu>();
}
