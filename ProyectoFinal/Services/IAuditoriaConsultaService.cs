using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record AuditoriaListado(IReadOnlyList<AuditoriaOperacion> Registros, IReadOnlyDictionary<string, string> Usuarios);

public interface IAuditoriaConsultaService
{
    Task<AuditoriaListado> ListarAsync(string? tipoEntidad, int? idEntidad);
}
