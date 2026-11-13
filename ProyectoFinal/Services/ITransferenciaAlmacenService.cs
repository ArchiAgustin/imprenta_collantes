using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface ITransferenciaAlmacenService
{
    Task<TransferenciasInventarioViewModel> ObtenerPanelAsync();
    Task<ResultadoTransferenciaInventario> TransferirAsync(int idOrigen, int idDestino, int idProducto,
        decimal cantidad, string motivo, string? usuario, ClaimsPrincipal actor);
}
