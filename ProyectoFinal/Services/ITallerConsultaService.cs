using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface ITallerConsultaService
{
    Task<IReadOnlyList<Pedido>> ListarAsync();
    Task<Pedido?> ObtenerAsync(int id);
}
