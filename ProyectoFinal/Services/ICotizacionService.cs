using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface ICotizacionService
{
    Task<List<Cotizacion>> ListarAsync();
    Task<Cotizacion?> ObtenerConLineasAsync(int id);
    Task<Cotizacion?> ObtenerParaEdicionAsync(int id);
    Task<List<Almacen>> ObtenerAlmacenesVentaAsync();
    Task<bool> TieneVentaAsync(int id);
    Task<bool> TieneVentaOPedidoAsync(int id);
    Task<List<CotizacionFormError>> ValidarFormularioAsync(CotizacionFormViewModel model);
    Task CargarRecursosFormularioAsync(CotizacionFormViewModel model);
    Task<ResultadoOperacionCotizacion> CrearAsync(CotizacionFormViewModel model, string userId, ClaimsPrincipal user);
    Task<ResultadoOperacionCotizacion> ActualizarAsync(int id, CotizacionFormViewModel model, ClaimsPrincipal user);
    Task<ResultadoOperacionCotizacion> EliminarAsync(int id, ClaimsPrincipal user);
    Task RegistrarEnvioPorCorreoAsync(int id, ClaimsPrincipal user);
}

public sealed record CotizacionFormError(string Campo, string Mensaje);

public enum EstadoOperacionCotizacion
{
    Exito,
    NoEncontrada,
    Conflicto,
    Vinculada,
    DatosNoDisponibles
}

public sealed record ResultadoOperacionCotizacion(
    EstadoOperacionCotizacion Estado,
    string Mensaje,
    int? IdCotizacion = null)
{
    public bool Exito => Estado == EstadoOperacionCotizacion.Exito;
}
