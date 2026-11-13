using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface IPermisosMatrizService
{
    Task<List<MenuSistema>> GetRoutableMenusAsync(bool tracking = false);
    Task<Dictionary<int, PermisoRolMenu>> ObtenerActualesAsync(string roleId);
    Task GuardarAsync(string roleId, IReadOnlyList<MenuSistema> menus, IReadOnlyList<MenuPermisoItemViewModel> submitted);
}
