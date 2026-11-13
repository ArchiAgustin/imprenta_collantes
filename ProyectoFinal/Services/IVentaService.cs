using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface IVentaService
{
    Task<IReadOnlyList<Venta>> ListarAsync();
    Task<Venta?> ObtenerDetalleAsync(int id);
    Task<Venta?> ObtenerParaImprimirAsync(int id);
    Task<ResultadoVenta> ConfirmarCotizacionAsync(int cotizacionId, string userId, ClaimsPrincipal user);
    Task<ResultadoVenta> ConfirmarCotizacionAsync(int cotizacionId, int idAlmacen, string userId, ClaimsPrincipal user);
    Task<ResultadoVenta> CancelarAsync(int ventaId, ClaimsPrincipal user);
    Task<ResultadoVenta> AvanzarPedidoAsync(int pedidoId, string nuevoEstado, ClaimsPrincipal user);
}
