using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class PreCotizacionCatalogoService(ApplicationDbContext db) : IPreCotizacionCatalogoService
{
    public async Task CargarAsync(PreCotizacionViewModel model)
    {
        model.Clientes = await db.Cliente.AsNoTracking().Include(client => client.DocumentoIdentidadTipo)
            .OrderBy(client => client.Nombre).ToListAsync();
        model.Productos = await db.Producto.AsNoTracking()
            .Include(product => product.UnidadMedidaBase)
            .Include(product => product.Presentaciones.Where(presentation => presentation.Activo && presentation.UnidadMedida.Activo))
            .ThenInclude(presentation => presentation.UnidadMedida)
            .OrderBy(product => product.Nombre).ToListAsync();
        model.ClienteIdSugerido = null;
        if (!model.ConfirmarIdentidadLocal || model.ClienteDocumentoTipoSugerido is not { } documentType
            || string.IsNullOrWhiteSpace(model.ClienteDocumentoNumeroSugerido)) return;
        var match = model.Clientes.FirstOrDefault(client =>
            client.DocumentoIdentidadTipo?.Codigo == documentType
                && client.NumeroDocumento == model.ClienteDocumentoNumeroSugerido
            || documentType == "1" && client.NumeroDocumento is null && client.Dni == model.ClienteDocumentoNumeroSugerido);
        model.ClienteIdSugerido = match?.IdCliente;
        if (model.ClienteId is null && match is not null) model.ClienteId = match.IdCliente;
    }
}
