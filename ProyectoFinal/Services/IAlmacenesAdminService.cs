using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ResultadoAlmacenAdmin(bool Encontrado, bool Exito, string Mensaje);

public interface IAlmacenesAdminService
{
    Task<IReadOnlyList<AlmacenAdminRowViewModel>> ListarAsync();
    Task<Almacen?> ObtenerAsync(int id);
    Task<bool> NombreExisteAsync(string nombre, int? exceptoId);
    Task<ResultadoAlmacenAdmin> GuardarAsync(AlmacenFormViewModel model, ClaimsPrincipal user);
    Task<ResultadoAlmacenAdmin> CambiarEstadoAsync(int id, ClaimsPrincipal user);
}
