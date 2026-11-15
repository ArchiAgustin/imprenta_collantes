using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("ConfiguracionGroq")]
public sealed class ConfiguracionGroq
{
    [Key, Column("id")]
    public int Id { get; set; } = 1;

    [Required, StringLength(2000), Column("apiKeyProtegida")]
    public string ApiKeyProtegida { get; set; } = string.Empty;

    [Column("actualizadoUtc")]
    public DateTimeOffset ActualizadoUtc { get; set; }
}
