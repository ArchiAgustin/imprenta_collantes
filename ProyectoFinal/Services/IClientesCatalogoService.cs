using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record PaginaCatalogo<T>(IReadOnlyList<T> Items, int Pagina, int Paginas, int Total);
public enum ResultadoEliminarCatalogo { Eliminado, NoEncontrado, EnUso }

public interface IClientesCatalogoService
{
    Task<PaginaCatalogo<Cliente>> ListarAsync(string? nombre, string? documento, string? correo, string? telefono, int pagina);
    Task<Cliente?> ObtenerAsync(int id);
    Task<Cliente?> ObtenerParaEliminarAsync(int id);
    Task CrearAsync(Cliente cliente, bool esDni);
    Task<bool> ActualizarAsync(int id, Cliente cambios, bool esDni);
    Task<ResultadoEliminarCatalogo> EliminarAsync(int id);
    Task<bool> ExisteAsync(int id);
}
