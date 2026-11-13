using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ProductoBusquedaInventario(int idProducto, string? nombre, string unidadBase, decimal stockActual);

public interface IInventarioConsultaService
{
    Task<InventarioIndexViewModel> ObtenerInicioAsync();
    Task<bool> AlmacenActivoAsync(int id);
    Task<IReadOnlyList<ProductoBusquedaInventario>> BuscarProductosAsync(string termino, int almacenId);
    Task<Inventario?> ObtenerDetalleAsync(int id);
    Task<KardexInventarioViewModel?> ObtenerKardexAsync(int? almacenId);
}
