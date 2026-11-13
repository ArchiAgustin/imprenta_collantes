using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface ITramosDescuentoService
{
    Task<Producto?> ObtenerProductoAsync(int productoId);
    Task<IReadOnlyList<TramoDescuentoProducto>> ListarAsync(int productoId);
    Task<bool> AgregarAsync(int productoId, int cantidadMinima, decimal descuentoPorcentaje);
    Task<bool> EliminarAsync(int productoId, int tramoId);
}
