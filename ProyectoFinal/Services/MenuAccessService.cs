using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public enum MenuPermissionKind
{
    View,
    Create,
    Edit,
    Delete,
    Process
}

public sealed class MenuNavigationItem
{
    public int IdMenu { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? ControllerNombre { get; init; }
    public string? ActionNombre { get; init; }
    public string? Icono { get; init; }
    public int Orden { get; init; }
    public bool PuedeNavegar { get; set; }
    public List<MenuNavigationItem> Children { get; } = new();
}

public interface IMenuAccessService
{
    bool RequiresMenuPermission(string controller, string action);
    Task<bool> CanAsync(ClaimsPrincipal user, string controller, string action, string httpMethod);
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string menuCode, MenuPermissionKind permission);
    Task<bool> HasDestinationPermissionAsync(ClaimsPrincipal user, string destination, MenuPermissionKind permission);
    Task<IReadOnlyList<MenuNavigationItem>> GetNavigationMenusAsync(ClaimsPrincipal user);
}

public sealed class MenuAccessService(ApplicationDbContext context) : IMenuAccessService
{
    public bool RequiresMenuPermission(string controller, string action) =>
        MenuDestinationRegistry.HasController(controller)
        && !(controller.Equals("CatalogosSunat", StringComparison.OrdinalIgnoreCase)
            && action.Equals("CambiarEstado", StringComparison.OrdinalIgnoreCase));

    public async Task<bool> CanAsync(ClaimsPrincipal user, string controller, string action, string httpMethod)
    {
        if (!MenuDestinationRegistry.TryGetForRoute(controller, action, out var destination)
            && !MenuDestinationRegistry.TryGetForController(controller, out destination)) return true;
        if (user.IsInRole("Admin")) return true;

        var kind = controller.Equals("PreCotizacion", StringComparison.OrdinalIgnoreCase)
            || controller.Equals("Cotizacions", StringComparison.OrdinalIgnoreCase)
                && action.Equals("EnviarPorCorreo", StringComparison.OrdinalIgnoreCase)
            ? MenuPermissionKind.Create
            : action switch
        {
            "Create" => MenuPermissionKind.Create,
            "Edit" or "AgregarPresentacion" or "CambiarEstadoPresentacion" => MenuPermissionKind.Edit,
            "Delete" => MenuPermissionKind.Delete,
            "ConfirmarDesdeCotizacion" or "Cancelar" or "ActualizarEstado" => MenuPermissionKind.Process,
            _ when HttpMethods.IsGet(httpMethod) || HttpMethods.IsHead(httpMethod) => MenuPermissionKind.View,
            _ => MenuPermissionKind.Process
        };
        return await HasDestinationPermissionAsync(user, destination.Codigo, kind);
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string menuCode, MenuPermissionKind permission)
    {
        if (user.IsInRole("Admin")) return true;
        if (user.Identity?.IsAuthenticated != true) return false;

        var roleNames = ActiveRoleNames(user);
        if (roleNames.Length == 0) return false;
        var query = context.PermisoRolMenu.AsNoTracking()
            .Where(item => item.Menu != null && item.Menu.Codigo == menuCode && item.Menu.Activo
                && item.Rol != null && item.Rol.Name != null && roleNames.Contains(item.Rol.Name));
        return await HasKindAsync(query, permission);
    }

    public async Task<IReadOnlyList<MenuNavigationItem>> GetNavigationMenusAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return Array.Empty<MenuNavigationItem>();

        var catalog = await context.MenuSistema.AsNoTracking()
            .OrderBy(menu => menu.Orden).ThenBy(menu => menu.IdMenu).ToListAsync();
        var catalogById = catalog.ToDictionary(menu => menu.IdMenu);
        var rows = catalog.Where(menu => menu.Activo && HasActiveAncestors(menu, catalogById)).ToList();
        if (catalog.Count == 0 && user.IsInRole("Admin")) return FallbackTree();

        var isAdmin = user.IsInRole("Admin");
        var roleNames = ActiveRoleNames(user);
        var permittedIds = isAdmin
            ? rows.Select(menu => menu.IdMenu).ToHashSet()
            : (await context.PermisoRolMenu.AsNoTracking()
                .Where(permission => permission.PuedeVer && permission.Menu != null && permission.Menu.Activo
                    && permission.Rol != null && permission.Rol.Name != null && roleNames.Contains(permission.Rol.Name))
                .Select(permission => permission.MenuSistemaId).ToListAsync()).ToHashSet();

        var nodes = rows.ToDictionary(menu => menu.IdMenu, menu =>
        {
            var destinationCode = menu.Destino ?? MenuDestinationRegistry.LegacyDestination(menu.Codigo);
            var hasDestination = MenuDestinationRegistry.TryGet(destinationCode, out var destination);
            var directlyPermitted = isAdmin || permittedIds.Contains(menu.IdMenu);
            return new MenuNavigationItem
            {
                IdMenu = menu.IdMenu,
                Codigo = menu.Codigo,
                Nombre = menu.Nombre,
                Icono = menu.Icono,
                Orden = menu.Orden,
                ControllerNombre = hasDestination ? destination.Controller : null,
                ActionNombre = hasDestination ? destination.Action : null,
                PuedeNavegar = hasDestination && directlyPermitted
            };
        });

        var roots = new List<MenuNavigationItem>();
        foreach (var row in rows)
        {
            var node = nodes[row.IdMenu];
            if (row.IdMenuPadre is { } parentId && nodes.TryGetValue(parentId, out var parent)) parent.Children.Add(node);
            else roots.Add(node);
        }

        if (isAdmin) return roots;
        return roots.Where(Prune).ToList();

        static bool Prune(MenuNavigationItem node)
        {
            for (var index = node.Children.Count - 1; index >= 0; index--)
                if (!Prune(node.Children[index])) node.Children.RemoveAt(index);
            return node.PuedeNavegar || node.Children.Count > 0;
        }
    }

    public async Task<bool> HasDestinationPermissionAsync(ClaimsPrincipal user, string destination, MenuPermissionKind permission)
    {
        if (user.IsInRole("Admin")) return true;
        if (user.Identity?.IsAuthenticated != true) return false;
        var roleNames = ActiveRoleNames(user);
        if (roleNames.Length == 0) return false;

        var catalog = await context.MenuSistema.AsNoTracking().ToListAsync();
        var catalogById = catalog.ToDictionary(menu => menu.IdMenu);
        var menuIds = catalog.Where(menu => menu.Activo && HasActiveAncestors(menu, catalogById)
                && (menu.Destino == destination || menu.Destino == null && menu.Codigo == destination))
            .Select(menu => menu.IdMenu).ToHashSet();

        // Existing registrations used Cotizaciones as the parent permission for its
        // AI-assisted pre-quotation workflow. Preserve that grant if the new leaf is
        // absent (for example, in an unmigrated test database).
        var precotizacionIsRegistered = catalog.Any(menu =>
            menu.Destino == "precotizacion" || menu.Destino == null && menu.Codigo == "precotizacion");
        if (destination == "precotizacion" && !precotizacionIsRegistered)
            menuIds.UnionWith(catalog.Where(menu => menu.Activo && HasActiveAncestors(menu, catalogById)
                && (menu.Destino == "cotizaciones" || menu.Destino == null && menu.Codigo == "cotizaciones"))
                .Select(menu => menu.IdMenu));

        var query = context.PermisoRolMenu.AsNoTracking()
            .Where(item => menuIds.Contains(item.MenuSistemaId)
                && item.Rol != null && item.Rol.Name != null && roleNames.Contains(item.Rol.Name));
        return await HasKindAsync(query, permission);
    }

    private static Task<bool> HasKindAsync(IQueryable<PermisoRolMenu> permissions, MenuPermissionKind permission) => permission switch
    {
        MenuPermissionKind.View => permissions.AnyAsync(item => item.PuedeVer),
        MenuPermissionKind.Create => permissions.AnyAsync(item => item.PuedeCrear),
        MenuPermissionKind.Edit => permissions.AnyAsync(item => item.PuedeEditar),
        MenuPermissionKind.Delete => permissions.AnyAsync(item => item.PuedeEliminar),
        MenuPermissionKind.Process => permissions.AnyAsync(item => item.PuedeProcesar),
        _ => Task.FromResult(false)
    };

    private static string[] ActiveRoleNames(ClaimsPrincipal user) => user.FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool HasActiveAncestors(MenuSistema menu, IReadOnlyDictionary<int, MenuSistema> byId)
    {
        var visited = new HashSet<int> { menu.IdMenu };
        var parentId = menu.IdMenuPadre;
        while (parentId is { } id)
        {
            if (!visited.Add(id) || !byId.TryGetValue(id, out var parent) || !parent.Activo) return false;
            parentId = parent.IdMenuPadre;
        }
        return true;
    }

    private static IReadOnlyList<MenuNavigationItem> FallbackTree()
    {
        var id = 0;
        MenuNavigationItem Group(string code, string name, int order) =>
            new() { IdMenu = --id, Codigo = code, Nombre = name, Orden = order };
        var admin = Group("administracion", "Administración", 10);
        var commercial = Group("comercial", "Comercial", 40);
        var warehouse = Group("almacen", "Almacén", 30);
        var access = Group("accesos", "Accesos", 10);
        var configuration = Group("configuracion", "Configuración", 20);
        var sunat = Leaf("catalogos-sunat", 20);
        var sunatIdentity = Group("sunat-identidad", "Identidad", 10);
        var sunatDocuments = Group("sunat-documentos", "Comprobantes, pagos y tributos", 20);
        var sunatLocations = Group("sunat-ubicacion", "Ubicación", 30);
        var sunatEntities = Group("sunat-entidades", "Empresas y entidades", 40);

        MenuNavigationItem Leaf(string code, int order)
        {
            var destination = MenuDestinationRegistry.All.Single(item => item.Codigo == code);
            return new MenuNavigationItem
            {
                IdMenu = --id,
                Codigo = code, Nombre = destination.Nombre, ControllerNombre = destination.Controller,
                ActionNombre = destination.Action, Orden = order, PuedeNavegar = true
            };
        }

        commercial.Children.AddRange(new[] { Leaf("clientes", 10), Leaf("proveedores", 20), Leaf("cotizaciones", 30), Leaf("ventas", 40), Leaf("taller", 50) });
        commercial.Children.Single(item => item.Codigo == "cotizaciones").Children.Add(Leaf("precotizacion", 10));
        warehouse.Children.AddRange(new[]
        {
            Leaf("almacenes", 10), Leaf("productos", 20), Leaf("inventario", 30),
            Leaf("inventario-kardex", 40), Leaf("transferencias", 50)
        });
        access.Children.AddRange(new[] { Leaf("usuarios", 10), Leaf("roles", 20), Leaf("permisos", 30) });
        configuration.Children.AddRange(new[] { Leaf("menus", 10), Leaf("series", 20) });
        admin.Children.AddRange(new[] { access, configuration, Leaf("auditoria", 30), Leaf("analitica", 40) });
        sunat.Children.AddRange(new[] { sunatIdentity, sunatDocuments, sunatLocations, sunatEntities });
        sunatIdentity.Children.AddRange(new[] { Leaf("sunat-documentos-identidad", 10), Leaf("sunat-tipo-contribuyente", 20) });
        sunatDocuments.Children.AddRange(new[]
        {
            Leaf("sunat-tipos-comprobante", 10), Leaf("sunat-monedas", 20), Leaf("sunat-unidades-medida", 30),
            Leaf("sunat-medios-pago", 40), Leaf("sunat-detracciones", 50), Leaf("sunat-motivos-nc", 60),
            Leaf("sunat-motivos-nd", 70), Leaf("sunat-motivos-traslado", 80), Leaf("sunat-estados-envio", 90)
        });
        sunatLocations.Children.AddRange(new[] { Leaf("sunat-ubigeos", 10), Leaf("sunat-tipo-via", 20), Leaf("sunat-tipo-zona", 30) });
        sunatEntities.Children.AddRange(new[]
        {
            Leaf("sunat-tipo-actividad", 10), Leaf("sunat-tipos-entidad-financiera", 20), Leaf("sunat-entidades-financieras", 30)
        });
        return new[] { admin, sunat, warehouse, commercial };
    }
}
