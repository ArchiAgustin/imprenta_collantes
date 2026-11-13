using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface IMenusAdminService
{
    Task<List<MenuSistema>> ListarAsync(bool tracking = false);
    Task<MenuSistema?> ObtenerAsync(int id, bool tracking = false);
    Task<bool> CodigoExisteAsync(string codigo, int? exceptoId);
    Task<bool> DestinoExisteAsync(string destino, int? exceptoId);
    Task<ITransaccionIdentidad?> IniciarTransaccionAsync();
    Task<MenuSistema> GuardarAsync(MenuSistema? item, MenuAdminFormViewModel model, ClaimsPrincipal user);
    Task CambiarEstadoAsync(MenuSistema item, ClaimsPrincipal user);
}
