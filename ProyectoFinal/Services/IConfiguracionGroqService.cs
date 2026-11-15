namespace ProyectoFinal.Services;

public interface IConfiguracionGroqService
{
    Task<bool> TieneClaveAsync();
    Task GuardarClaveAsync(string apiKey);
    Task<string?> ObtenerClaveAsync();
}
