namespace ProyectoFinal.Models;

public sealed class RolAdminListViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public int UsuariosAsignados { get; init; }
    public bool EsProtegido { get; init; }
    public bool PuedeEliminar => !EsProtegido && UsuariosAsignados == 0;
}
