namespace ProyectoFinal.Models;

public sealed class UsuarioAdminListViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
