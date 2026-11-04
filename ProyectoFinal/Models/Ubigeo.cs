using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("ubigeo")]
public sealed class Ubigeo
{
    [Key]
    [Column("idubigeo")]
    public int IdUbigeo { get; set; }

    [Required, StringLength(6, MinimumLength = 6)]
    [Column("ubigeo", TypeName = "char(6)")]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [Required, StringLength(6)]
    [Column("parent", TypeName = "char(6)")]
    public string Parent { get; set; } = string.Empty;

    [Column("idpais")]
    public int? IdPais { get; set; }

    [Column("idparent")]
    public int? IdParent { get; set; }
}
