namespace ProyectoFinal.Services;

public sealed record MenuDestination(string Codigo, string Nombre, string Controller, string Action = "Index");

/// <summary>
/// A menu may point only at one of these reviewed MVC destinations. Database data
/// stores the stable code, never a user-supplied URL, controller or action.
/// </summary>
public static class MenuDestinationRegistry
{
    public static IReadOnlyList<MenuDestination> All { get; } = new[]
    {
        new MenuDestination("clientes", "Clientes", "Clientes"),
        new MenuDestination("proveedores", "Proveedores", "Proveedors"),
        new MenuDestination("productos", "Productos", "Productoes"),
        new MenuDestination("cotizaciones", "Cotizaciones", "Cotizacions"),
        new MenuDestination("precotizacion", "Pre-cotización con IA", "PreCotizacion"),
        new MenuDestination("ventas", "Ventas", "Ventas"),
        new MenuDestination("taller", "Taller", "Taller"),
        new MenuDestination("inventario", "Inventario", "Inventario"),
        new MenuDestination("inventario-kardex", "Kárdex reciente", "Inventario", "Kardex"),
        new MenuDestination("almacenes", "Almacenes", "Almacenes"),
        new MenuDestination("transferencias", "Transferencias", "Transferencias"),
        new MenuDestination("auditoria", "Auditoría", "Auditoria"),
        new MenuDestination("analitica", "Analítica", "Analytics"),
        new MenuDestination("usuarios", "Usuarios", "Admin"),
        new MenuDestination("roles", "Roles", "Roles"),
        new MenuDestination("permisos", "Accesos por rol", "Permisos"),
        new MenuDestination("menus", "Menús", "Menus"),
        new MenuDestination("parametros", "Parámetros", "Parametros"),
        new MenuDestination("series", "Series", "SeriesInternas"),
        new MenuDestination("catalogos-sunat", "Catálogos SUNAT", "CatalogosSunat"),
        new MenuDestination("sunat-documentos-identidad", "Tipos de documento de identidad", "CatalogosSunat", "TiposDocumentoIdentidad"),
        new MenuDestination("sunat-tipos-comprobante", "Tipos de comprobante", "CatalogosSunat", "TiposComprobante"),
        new MenuDestination("sunat-monedas", "Monedas", "CatalogosSunat", "Monedas"),
        new MenuDestination("sunat-unidades-medida", "Unidades de medida", "CatalogosSunat", "UnidadesMedida"),
        new MenuDestination("sunat-ubigeos", "Ubigeos", "CatalogosSunat", "Ubigeos"),
        new MenuDestination("sunat-detracciones", "Detracciones", "CatalogosSunat", "Detracciones"),
        new MenuDestination("sunat-medios-pago", "Medios de pago", "CatalogosSunat", "MediosPago"),
        new MenuDestination("sunat-motivos-nc", "Motivos de nota de crédito", "CatalogosSunat", "MotivosNotaCredito"),
        new MenuDestination("sunat-motivos-nd", "Motivos de nota de débito", "CatalogosSunat", "MotivosNotaDebito"),
        new MenuDestination("sunat-motivos-traslado", "Motivos de traslado", "CatalogosSunat", "MotivosTraslado"),
        new MenuDestination("sunat-tipos-entidad-financiera", "Tipos de entidad financiera", "CatalogosSunat", "TiposEntidadFinanciera"),
        new MenuDestination("sunat-entidades-financieras", "Entidades financieras", "CatalogosSunat", "EntidadesFinancieras"),
        new MenuDestination("sunat-tipo-actividad", "Tipos de actividad económica", "CatalogosSunat", "TiposActividad"),
        new MenuDestination("sunat-tipo-contribuyente", "Tipos de contribuyente", "CatalogosSunat", "TiposContribuyente"),
        new MenuDestination("sunat-tipo-via", "Tipos de vía", "CatalogosSunat", "TiposVia"),
        new MenuDestination("sunat-tipo-zona", "Tipos de zona", "CatalogosSunat", "TiposZona"),
        new MenuDestination("sunat-estados-envio", "Estados de envío SUNAT", "CatalogosSunat", "EstadosEnvio")
    };

    private static readonly IReadOnlyDictionary<string, MenuDestination> ByCode =
        All.ToDictionary(item => item.Codigo, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, MenuDestination> ByRoute =
        All.ToDictionary(item => RouteKey(item.Controller, item.Action), StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, MenuDestination> ByController =
        All.Where(item => item.Action == "Index")
            .ToDictionary(item => item.Controller, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> LegacyCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["clientes"] = "clientes", ["proveedores"] = "proveedores", ["productos"] = "productos",
        ["cotizaciones"] = "cotizaciones", ["ventas"] = "ventas", ["taller"] = "taller",
        ["inventario"] = "inventario", ["auditoria"] = "auditoria", ["analytics"] = "analitica",
        ["usuarios"] = "usuarios", ["permisos"] = "permisos", ["series-internas"] = "series"
    };

    public static bool TryGet(string? codigo, out MenuDestination destination)
    {
        if (codigo is not null && ByCode.TryGetValue(codigo, out destination!)) return true;
        destination = null!;
        return false;
    }

    public static bool TryGetForController(string controller, out MenuDestination destination) =>
        ByController.TryGetValue(controller, out destination!);

    public static bool HasController(string controller) => ByController.ContainsKey(controller);

    public static bool TryGetForRoute(string controller, string action, out MenuDestination destination) =>
        ByRoute.TryGetValue(RouteKey(controller, action), out destination!);

    public static string? LegacyDestination(string? codigo) =>
        codigo is not null && LegacyCodes.TryGetValue(codigo, out var destination) ? destination : null;

    public static string? ControllerFor(string? destination) =>
        TryGet(destination, out var item) ? item.Controller : null;

    public static string? ActionFor(string? destination) =>
        TryGet(destination, out var item) ? item.Action : null;

    private static string RouteKey(string controller, string action) => $"{controller}/{action}";
}
