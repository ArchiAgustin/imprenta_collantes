using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("SerieInventario")]
public sealed class SerieInventario
{
    [Key, Column("idSerieInventario")]
    public int IdSerieInventario { get; set; }

    [Required, StringLength(8), RegularExpression("^[A-Z0-9]{1,8}$"), Column("serie")]
    public string Serie { get; set; } = "INV1";

    [Range(1, 99_999_999), Column("siguienteCorrelativo")]
    public int SiguienteCorrelativo { get; set; } = 1;

    [Range(8, 8), Column("digitos")]
    public int Digitos { get; set; } = 8;

    [Column("activa")]
    public bool Activa { get; set; } = true;
}
