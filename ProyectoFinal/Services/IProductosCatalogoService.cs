using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public enum ResultadoProductoEstado { Exito, NoEncontrado, Invalido, Conflicto, EnUso }

public sealed record ResultadoProducto(ResultadoProductoEstado Estado, Producto? Producto = null,
    IReadOnlyDictionary<string, string>? Errores = null);

public sealed record ListadoProductos(IReadOnlyList<Producto> Productos, IReadOnlyList<Almacen> Almacenes,
    Almacen? AlmacenSeleccionado, IReadOnlyDictionary<int, decimal> Saldos);

public sealed record DetalleProducto(Producto Producto, IReadOnlyList<Almacen> Almacenes,
    Almacen? AlmacenSeleccionado, string StockActual, IReadOnlyList<PresentacionProducto> Presentaciones,
    IReadOnlyList<UnidadMedida> Unidades);

public interface IProductosCatalogoService
{
    Task<ListadoProductos?> ListarAsync(int? almacenId);
    Task<DetalleProducto?> ObtenerDetalleAsync(int id, int? almacenId);
    Task<StockPorAlmacenViewModel?> ObtenerStockPorAlmacenAsync(int id);
    Task<Producto?> ObtenerAsync(int id);
    Task<Producto?> ObtenerParaEliminarAsync(int id);
    Task<(IReadOnlyList<Proveedor> Proveedores, IReadOnlyList<UnidadMedida> Unidades)> ObtenerOpcionesAsync();
    Task<ResultadoProducto> CrearAsync(Producto producto);
    Task<ResultadoProducto> AgregarPresentacionAsync(int id, PresentacionProductoFormViewModel model, ClaimsPrincipal user);
    Task<ResultadoProducto> CambiarEstadoPresentacionAsync(int id, int presentacionId, ClaimsPrincipal user);
    Task<ResultadoProducto> ActualizarAsync(int id, Producto producto);
    Task<ResultadoProducto> EliminarAsync(int id);
}
