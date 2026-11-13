using System.Data;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ResultadoCambioCatalogo(bool Exito, string Mensaje);

public sealed class CatalogoSunatService(ApplicationDbContext db)
{
    public const int FilasPorPagina = 50;

    public async Task<CatalogoSunatPaginaViewModel?> ObtenerPaginaAsync(string codigo, string? busqueda, int pagina)
    {
        if (!CatalogoSunatRegistry.TryGet(codigo, out var catalogo)) return null;
        var rows = await CargarFilasAsync(codigo);
        var query = busqueda?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            rows = rows.Where(row => Contains(row.Codigo, query) || Contains(row.Descripcion, query)
                    || Contains(row.Detalle, query) || row.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        rows = rows.OrderBy(row => row.Codigo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Descripcion, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Id).ToList();
        var pages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)FilasPorPagina));
        var currentPage = Math.Clamp(pagina, 1, pages);
        return new CatalogoSunatPaginaViewModel
        {
            Catalogo = catalogo,
            Busqueda = query,
            Pagina = currentPage,
            Paginas = pages,
            Total = rows.Count,
            Filas = rows.Skip((currentPage - 1) * FilasPorPagina).Take(FilasPorPagina).ToList()
        };
    }

    public async Task<ResultadoCambioCatalogo> CambiarEstadoAsync(
        string codigo,
        int id,
        bool activo,
        System.Security.Claims.ClaimsPrincipal actor)
    {
        if (!CatalogoSunatRegistry.TryGet(codigo, out var catalogo) || !catalogo.PermiteActivar)
            return new(false, "Este catálogo es de consulta y no admite cambios de estado.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        bool estadoAnterior;
        string descripcion;
        switch (codigo)
        {
            case "documentos-identidad":
                var documento = await db.DocumentoIdentidadTipos.SingleOrDefaultAsync(item => item.IdDocumentoIdentidadTipo == id);
                if (documento is null) return new(false, "No se encontró el tipo de documento.");
                if (documento.Eliminado) return new(false, "El registro está dado de baja lógica; no se modifica desde este módulo.");
                if (!activo && await db.Cliente.AnyAsync(item => item.IdDocumentoIdentidadTipo == id))
                    return new(false, "No se puede desactivar: hay clientes vinculados a este tipo de documento.");
                estadoAnterior = documento.Estado;
                descripcion = documento.Nombre;
                documento.Estado = activo;
                break;

            case "unidades-medida":
                var unidad = await db.UnidadesMedida.SingleOrDefaultAsync(item => item.IdUnidadMedida == id);
                if (unidad is null) return new(false, "No se encontró la unidad de medida.");
                if (!activo && (await db.Producto.AnyAsync(item => item.IdUnidadMedidaBase == id)
                    || await db.PresentacionesProducto.AnyAsync(item => item.IdUnidadMedida == id)))
                    return new(false, "No se puede desactivar: hay productos o presentaciones que usan esta unidad.");
                estadoAnterior = unidad.Activo;
                descripcion = unidad.Descripcion;
                unidad.Activo = activo;
                break;

            case "ubigeos":
                return new(false, "El catálogo de ubigeos es de consulta; no se puede activar ni desactivar desde aquí.");

            case "tipos-entidad-financiera":
                var tipoEntidad = await db.TiposEntidadFinancieraSunat.SingleOrDefaultAsync(item => item.IdTipoEntidadFinanciera == id);
                if (tipoEntidad is null) return new(false, "No se encontró el tipo de entidad financiera.");
                if (!activo && await db.EntidadesFinancierasSunat.AnyAsync(item => item.IdTipoEntidadFinanciera == id))
                    return new(false, "No se puede desactivar: hay entidades financieras vinculadas a este tipo.");
                estadoAnterior = tipoEntidad.Estado;
                descripcion = tipoEntidad.Descripcion;
                tipoEntidad.Estado = activo;
                break;

            case "tipos-comprobante":
                var comprobante = await db.TiposDocumentoPagoSunat.SingleOrDefaultAsync(item => item.IdTipoDocumentoPago == id);
                if (comprobante is null) return new(false, "No se encontró el tipo de comprobante.");
                estadoAnterior = comprobante.Estado;
                descripcion = comprobante.Descripcion;
                comprobante.Estado = activo;
                break;

            case "monedas":
                var moneda = await db.MonedasSunat.SingleOrDefaultAsync(item => item.IdMoneda == id);
                if (moneda is null) return new(false, "No se encontró la moneda.");
                estadoAnterior = moneda.Estado;
                descripcion = moneda.Descripcion;
                moneda.Estado = activo;
                break;

            case "detracciones":
                var detraccion = await db.DetraccionesSunat.SingleOrDefaultAsync(item => item.IdDetraccion == id);
                if (detraccion is null) return new(false, "No se encontró el código de detracción.");
                estadoAnterior = detraccion.Estado;
                descripcion = detraccion.Descripcion;
                detraccion.Estado = activo;
                break;

            case "medios-pago":
                var medio = await db.TiposMedioPagoSunat.SingleOrDefaultAsync(item => item.IdTipoMedioPago == id);
                if (medio is null) return new(false, "No se encontró el medio de pago.");
                estadoAnterior = medio.Estado;
                descripcion = medio.Descripcion ?? "Medio de pago";
                medio.Estado = activo;
                break;

            case "motivos-nc":
                var motivoNc = await db.MotivosNotaCreditoSunat.SingleOrDefaultAsync(item => item.IdMotivoNotaCredito == id);
                if (motivoNc is null) return new(false, "No se encontró el motivo de nota de crédito.");
                estadoAnterior = motivoNc.Estado;
                descripcion = motivoNc.Descripcion;
                motivoNc.Estado = activo;
                break;

            case "motivos-nd":
                var motivoNd = await db.MotivosNotaDebitoSunat.SingleOrDefaultAsync(item => item.IdMotivoNotaDebito == id);
                if (motivoNd is null) return new(false, "No se encontró el motivo de nota de débito.");
                estadoAnterior = motivoNd.Estado;
                descripcion = motivoNd.Descripcion;
                motivoNd.Estado = activo;
                break;

            case "motivos-traslado":
                var traslado = await db.MotivosTrasladoSunat.SingleOrDefaultAsync(item => item.IdMotivoTraslado == id);
                if (traslado is null) return new(false, "No se encontró el motivo de traslado.");
                estadoAnterior = traslado.Estado;
                descripcion = traslado.Descripcion;
                traslado.Estado = activo;
                break;

            case "entidades-financieras":
                var entidad = await db.EntidadesFinancierasSunat.SingleOrDefaultAsync(item => item.IdEntidadFinanciera == id);
                if (entidad is null) return new(false, "No se encontró la entidad financiera.");
                estadoAnterior = entidad.Estado;
                descripcion = entidad.Descripcion;
                entidad.Estado = activo;
                break;

            case "tipos-actividad":
                var actividad = await db.TiposActividadSunat.SingleOrDefaultAsync(item => item.IdTipoActividad == id);
                if (actividad is null) return new(false, "No se encontró el tipo de actividad.");
                estadoAnterior = actividad.Estado;
                descripcion = actividad.Descripcion ?? "Tipo de actividad";
                actividad.Estado = activo;
                break;

            case "tipos-contribuyente":
                var contribuyente = await db.TiposContribuyenteSunat.SingleOrDefaultAsync(item => item.IdTipoContribuyente == id);
                if (contribuyente is null) return new(false, "No se encontró el tipo de contribuyente.");
                estadoAnterior = contribuyente.Estado;
                descripcion = contribuyente.Descripcion;
                contribuyente.Estado = activo;
                break;

            case "tipos-via":
                var via = await db.TiposViaSunat.SingleOrDefaultAsync(item => item.IdViaTipo == id);
                if (via is null) return new(false, "No se encontró el tipo de vía.");
                estadoAnterior = via.Estado;
                descripcion = via.Descripcion;
                via.Estado = activo;
                break;

            case "tipos-zona":
                var zona = await db.TiposZonaSunat.SingleOrDefaultAsync(item => item.IdZonaTipo == id);
                if (zona is null) return new(false, "No se encontró el tipo de zona.");
                estadoAnterior = zona.Estado;
                descripcion = zona.Descripcion;
                zona.Estado = activo;
                break;

            case "estados-envio":
                var estadoSunat = await db.EstadosEnvioSunat.SingleOrDefaultAsync(item => item.IdEstadoEnvioSunat == id);
                if (estadoSunat is null) return new(false, "No se encontró el estado de envío.");
                estadoAnterior = estadoSunat.Estado;
                descripcion = estadoSunat.Descripcion;
                estadoSunat.Estado = activo;
                break;

            default:
                return new(false, "Catálogo no reconocido.");
        }

        if (estadoAnterior == activo) return new(true, "El registro ya tenía el estado solicitado.");
        await db.SaveChangesAsync();
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(actor, $"CatalogoSunat:{codigo}", id,
            activo ? "Registro activado" : "Registro desactivado", estadoAnterior ? "Activo" : "Inactivo",
            activo ? "Activo" : "Inactivo", descripcion));
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, activo ? "Registro activado." : "Registro desactivado.");
    }

    private async Task<List<CatalogoSunatFilaViewModel>> CargarFilasAsync(string codigo) => codigo switch
    {
        "documentos-identidad" => (await db.DocumentoIdentidadTipos.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdDocumentoIdentidadTipo, item.Codigo, item.Nombre,
            $"{item.Abreviatura ?? ""}{(item.Longitud is null ? "" : $" · {item.Longitud} dígitos")}", item.Estado && !item.Eliminado,
            item.Eliminado, item.Eliminado ? "Baja lógica" : null)).ToList(),
        "tipos-comprobante" => (await db.TiposDocumentoPagoSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdTipoDocumentoPago, item.CodigoSunat, item.Descripcion,
            string.Join(" · ", new[] { item.DescripcionCorta, item.MostrarRc is null ? null : $"RC {(item.MostrarRc.Value ? "visible" : "oculta")}" }.Where(value => !string.IsNullOrWhiteSpace(value))), item.Estado)).ToList(),
        "monedas" => (await db.MonedasSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdMoneda, item.CodigoSunat, item.Descripcion,
            string.Join(" · ", new[] { item.CodigoIso, item.Simbolo, item.DescripcionCorta }.Where(value => !string.IsNullOrWhiteSpace(value))), item.Estado)).ToList(),
        "unidades-medida" => (await db.UnidadesMedida.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdUnidadMedida, item.CodigoSunat, item.Descripcion, item.DescripcionCorta, item.Activo)).ToList(),
        "ubigeos" => (await db.Ubigeos.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdUbigeo, item.Codigo, item.Descripcion, $"Superior: {item.Parent}", null, true, "Catálogo gestionado fuera de esta pantalla")).ToList(),
        "detracciones" => (await db.DetraccionesSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdDetraccion, item.Codigo, item.Descripcion, item.Porcentaje is null ? null : $"{item.Porcentaje:N2}%", item.Estado)).ToList(),
        "medios-pago" => (await db.TiposMedioPagoSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdTipoMedioPago, item.CodigoSunat, item.Descripcion ?? "(Sin descripción)",
            string.Join(" · ", new[] { item.DescripcionCorta, item.MonederoDigital == true ? "Monedero digital" : null, item.CuentaInterna == true ? "Cuenta interna" : null }.Where(value => !string.IsNullOrWhiteSpace(value))), item.Estado)).ToList(),
        "motivos-nc" => (await db.MotivosNotaCreditoSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdMotivoNotaCredito, item.CodigoSunat, item.Descripcion, null, item.Estado)).ToList(),
        "motivos-nd" => (await db.MotivosNotaDebitoSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdMotivoNotaDebito, item.CodigoSunat, item.Descripcion, null, item.Estado)).ToList(),
        "motivos-traslado" => (await db.MotivosTrasladoSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdMotivoTraslado, item.CodigoSunat, item.Descripcion, null, item.Estado)).ToList(),
        "tipos-entidad-financiera" => (await db.TiposEntidadFinancieraSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdTipoEntidadFinanciera, null, item.Descripcion, item.DescripcionCorta, item.Estado)).ToList(),
        "entidades-financieras" => (await db.EntidadesFinancierasSunat.AsNoTracking().Include(item => item.Tipo).ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(
            item.IdEntidadFinanciera, item.CodigoSunat, item.Descripcion,
            string.Join(" · ", new[] { item.DescripcionCorta, item.Tipo?.Descripcion }.Where(value => !string.IsNullOrWhiteSpace(value))), item.Estado)).ToList(),
        "tipos-actividad" => (await db.TiposActividadSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdTipoActividad, item.CodigoSunat, item.Descripcion ?? "(Sin descripción)", item.DescripcionCorta, item.Estado)).ToList(),
        "tipos-contribuyente" => (await db.TiposContribuyenteSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdTipoContribuyente, item.CodigoSunat, item.Descripcion, null, item.Estado)).ToList(),
        "tipos-via" => (await db.TiposViaSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdViaTipo, item.CodigoSunat, item.Descripcion, item.DescripcionCorta, item.Estado)).ToList(),
        "tipos-zona" => (await db.TiposZonaSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdZonaTipo, item.CodigoSunat, item.Descripcion, item.DescripcionCorta, item.Estado)).ToList(),
        "estados-envio" => (await db.EstadosEnvioSunat.AsNoTracking().ToListAsync()).Select(item => new CatalogoSunatFilaViewModel(item.IdEstadoEnvioSunat, item.IdEstadoEnvioSunat.ToString(), item.Descripcion, null, item.Estado)).ToList(),
        _ => new List<CatalogoSunatFilaViewModel>()
    };

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
}
