using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public static class CatalogoSunatRegistry
{
    public static IReadOnlyList<CatalogoSunatDefinicion> Catalogos { get; } =
    [
        new("documentos-identidad", "Tipos de documento de identidad", "Identidad", "Códigos de identificación usados por clientes y proveedores.", "TiposDocumentoIdentidad", true),
        new("tipos-comprobante", "Tipos de comprobante", "Comprobantes, pagos y tributos", "Catálogo de tipos de comprobante de pago.", "TiposComprobante", true),
        new("monedas", "Monedas", "Comprobantes, pagos y tributos", "Códigos, abreviaturas y símbolos monetarios.", "Monedas", true),
        new("unidades-medida", "Unidades de medida", "Comprobantes, pagos y tributos", "Unidades de medida vinculadas a productos.", "UnidadesMedida", true),
        new("medios-pago", "Medios de pago", "Comprobantes, pagos y tributos", "Códigos de medios de pago.", "MediosPago", true),
        new("detracciones", "Detracciones", "Comprobantes, pagos y tributos", "Códigos y porcentajes de detracción.", "Detracciones", true),
        new("motivos-nc", "Motivos de nota de crédito", "Comprobantes, pagos y tributos", "Motivos codificados para notas de crédito.", "MotivosNotaCredito", true),
        new("motivos-nd", "Motivos de nota de débito", "Comprobantes, pagos y tributos", "Motivos codificados para notas de débito.", "MotivosNotaDebito", true),
        new("motivos-traslado", "Motivos de traslado", "Comprobantes, pagos y tributos", "Motivos codificados para guías de remisión.", "MotivosTraslado", true),
        new("estados-envio", "Estados de envío SUNAT", "Comprobantes, pagos y tributos", "Estados operativos de envío y respuesta.", "EstadosEnvio", true),
        new("ubigeos", "Ubigeos", "Ubicación", "Departamento, provincia y distrito.", "Ubigeos", false),
        new("tipos-via", "Tipos de vía", "Ubicación", "Códigos de tipo de vía.", "TiposVia", true),
        new("tipos-zona", "Tipos de zona", "Ubicación", "Códigos de tipo de zona.", "TiposZona", true),
        new("tipos-actividad", "Tipos de actividad económica", "Empresas y entidades", "Clasificación de actividades económicas.", "TiposActividad", true),
        new("tipos-contribuyente", "Tipos de contribuyente", "Identidad", "Clasificación de contribuyentes.", "TiposContribuyente", true),
        new("tipos-entidad-financiera", "Tipos de entidad financiera", "Empresas y entidades", "Clasificación de entidades financieras.", "TiposEntidadFinanciera", true),
        new("entidades-financieras", "Entidades financieras", "Empresas y entidades", "Entidades y códigos asociados.", "EntidadesFinancieras", true)
    ];

    private static readonly IReadOnlyDictionary<string, CatalogoSunatDefinicion> ByCode =
        Catalogos.ToDictionary(item => item.Codigo, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, CatalogoSunatDefinicion> ByAction =
        Catalogos.ToDictionary(item => item.Accion, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string? codigo, out CatalogoSunatDefinicion catalogo)
    {
        if (codigo is not null && ByCode.TryGetValue(codigo, out catalogo!)) return true;
        catalogo = null!;
        return false;
    }

    public static bool TryGetByAction(string action, out CatalogoSunatDefinicion catalogo) =>
        ByAction.TryGetValue(action, out catalogo!);
}
