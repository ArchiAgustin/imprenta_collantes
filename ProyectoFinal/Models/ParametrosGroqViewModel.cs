using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class ParametrosGroqViewModel
{
    [DataType(DataType.Password), StringLength(256)]
    [Display(Name = "Clave API de Groq")]
    public string? ApiKey { get; set; }

    public bool TieneClaveGuardada { get; set; }
}
