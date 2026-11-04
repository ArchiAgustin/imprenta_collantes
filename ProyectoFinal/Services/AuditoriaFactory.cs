using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public static class AuditoriaFactory
{
    public static AuditoriaOperacion Crear(
        ClaimsPrincipal user,
        string tipoEntidad,
        int idEntidad,
        string accion,
        string? estadoAnterior = null,
        string? estadoNuevo = null,
        string? detalle = null) => new()
    {
        TipoEntidad = tipoEntidad,
        IdEntidad = idEntidad,
        Accion = accion,
        EstadoAnterior = estadoAnterior,
        EstadoNuevo = estadoNuevo,
        Detalle = detalle,
        IdUsuario = user.FindFirstValue(ClaimTypes.NameIdentifier),
        FechaUtc = DateTimeOffset.UtcNow
    };

    public static AuditoriaOperacion CrearSistema(
        string tipoEntidad,
        int idEntidad,
        string accion,
        string? estadoAnterior = null,
        string? estadoNuevo = null,
        string? detalle = null) => new()
    {
        TipoEntidad = tipoEntidad,
        IdEntidad = idEntidad,
        Accion = accion,
        EstadoAnterior = estadoAnterior,
        EstadoNuevo = estadoNuevo,
        Detalle = detalle,
        FechaUtc = DateTimeOffset.UtcNow
    };
}
