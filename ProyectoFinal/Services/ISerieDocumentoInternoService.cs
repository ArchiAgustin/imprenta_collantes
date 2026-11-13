using System.Security.Claims;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface ISerieDocumentoInternoService
{
    Task<IReadOnlyList<SerieDocumentoInterno>> ListarAsync();
    Task<SerieDocumentoInterno?> ObtenerAsync(int id);
    Task<IReadOnlyList<TipoDocumentoPagoSunat>> TiposComprobanteActivosAsync();
    Task<ResultadoSerie> GuardarAsync(int? id, string tipo, string codigo, int siguiente, int digitos,
        bool activar, ClaimsPrincipal user, int? idTipoComprobanteSunat = null);
}
