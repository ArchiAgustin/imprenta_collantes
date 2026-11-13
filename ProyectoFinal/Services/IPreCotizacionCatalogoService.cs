using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface IPreCotizacionCatalogoService
{
    Task CargarAsync(PreCotizacionViewModel model);
}
