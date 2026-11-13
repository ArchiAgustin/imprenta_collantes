using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface IProveedoresCatalogoService
{
    Task<PaginaCatalogo<Proveedor>> ListarAsync(string? nombre, string? documento, string? contacto, int pagina);
    Task<Proveedor?> ObtenerAsync(int id);
    Task<Proveedor?> ObtenerDetalleAsync(int id);
    Task<Proveedor?> ObtenerParaEliminarAsync(int id);
    Task CrearAsync(Proveedor proveedor);
    Task<bool> ActualizarAsync(int id, Proveedor cambios);
    Task<ResultadoEliminarCatalogo> EliminarAsync(int id);
    Task<bool> ExisteAsync(int id);
}
