using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface IParametrosCorreoService
{
    Task<ParametrosCorreoViewModel> ObtenerAsync();
    Task<bool> TieneContrasenaAsync();
    Task GuardarAsync(ParametrosCorreoViewModel model);
}
