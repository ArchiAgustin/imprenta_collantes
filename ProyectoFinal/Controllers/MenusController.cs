using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class MenusController(IMenusAdminService service) : Controller
{
    private const int MaxDepth = 2;
    private static readonly string[] AllowedIcons = ["grid", "shield", "layers", "users", "settings", "folder", "spark", "sliders", "bolt"];

    public async Task<IActionResult> Index()
    {
        var rows = await service.ListarAsync();
        var byId = rows.ToDictionary(item => item.IdMenu);
        var view = new List<MenuAdminItemViewModel>();
        var visited = new HashSet<int>();

        void Visit(MenuSistema item, int depth)
        {
            if (!visited.Add(item.IdMenu)) return;
            var destinationKey = item.Destino ?? MenuDestinationRegistry.LegacyDestination(item.Codigo);
            MenuDestinationRegistry.TryGet(destinationKey, out var destination);
            var parent = item.IdMenuPadre is { } parentId && byId.TryGetValue(parentId, out var parentRow) ? parentRow : null;
            view.Add(new MenuAdminItemViewModel
            {
                Id = item.IdMenu,
                Codigo = item.Codigo,
                Nombre = item.Nombre,
                Icono = item.Icono,
                Destino = item.Destino,
                DestinoNombre = destination?.Nombre,
                IdMenuPadre = item.IdMenuPadre,
                NombrePadre = parent?.Nombre,
                Orden = item.Orden,
                Profundidad = depth,
                Activo = item.Activo,
                TieneHijosActivos = rows.Any(child => child.IdMenuPadre == item.IdMenu && child.Activo)
            });
            foreach (var child in rows.Where(child => child.IdMenuPadre == item.IdMenu).OrderBy(child => child.Orden).ThenBy(child => child.IdMenu))
                Visit(child, depth + 1);
        }

        foreach (var root in rows.Where(item => item.IdMenuPadre is null).OrderBy(item => item.Orden).ThenBy(item => item.IdMenu)) Visit(root, 0);
        foreach (var item in rows) Visit(item, DepthOf(item, byId));
        ViewBag.Destinations = MenuDestinationRegistry.All;
        return View(view);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadParentsAsync();
        return View("Editar", new MenuAdminFormViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await service.ObtenerAsync(id);
        if (item is null) return NotFound();
        await LoadParentsAsync(id);
        return View("Editar", new MenuAdminFormViewModel
        {
            IdMenu = item.IdMenu,
            Codigo = item.Codigo,
            Nombre = item.Nombre,
            Icono = item.Icono,
            IdMenuPadre = item.IdMenuPadre,
            Orden = item.Orden,
            Destino = item.Destino ?? MenuDestinationRegistry.LegacyDestination(item.Codigo)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(MenuAdminFormViewModel model)
    {
        await using var transaction = await service.IniciarTransaccionAsync();
        model.Codigo = model.Codigo?.Trim().ToLowerInvariant() ?? string.Empty;
        model.Nombre = model.Nombre?.Trim() ?? string.Empty;
        model.Destino = string.IsNullOrWhiteSpace(model.Destino) ? null : model.Destino.Trim();
        model.Icono = string.IsNullOrWhiteSpace(model.Icono) ? null : model.Icono.Trim();

        var item = model.IdMenu is { } id ? await service.ObtenerAsync(id, tracking: true) : null;
        if (model.IdMenu is not null && item is null) return NotFound();
        if (item is not null && !string.Equals(item.Codigo, model.Codigo, StringComparison.Ordinal))
            ModelState.AddModelError(nameof(model.Codigo), "El código técnico es estable y no puede modificarse.");
        if (!Regex.IsMatch(model.Codigo, "^[a-z][a-z0-9-]{0,63}$"))
            ModelState.AddModelError(nameof(model.Codigo), "Usa un código único en minúsculas, iniciado con letra y compuesto por letras, números o guiones.");
        if (await service.CodigoExisteAsync(model.Codigo, model.IdMenu))
            ModelState.AddModelError(nameof(model.Codigo), "Ya existe un menú con este código.");
        if (model.Destino is not null && !MenuDestinationRegistry.TryGet(model.Destino, out _))
            ModelState.AddModelError(nameof(model.Destino), "Selecciona un destino registrado por la aplicación.");
        if (model.Icono is not null && !AllowedIcons.Contains(model.Icono, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(model.Icono), "Selecciona un icono disponible.");
        if (model.Orden is < 0 or > 9999)
            ModelState.AddModelError(nameof(model.Orden), "El orden debe estar entre 0 y 9999.");
        if (model.Destino is not null && await service.DestinoExisteAsync(model.Destino, model.IdMenu))
            ModelState.AddModelError(nameof(model.Destino), "Ese destino ya está asociado a otro menú; no se duplican accesos a una misma pantalla.");
        if (item is not null && !string.Equals(item.Destino ?? MenuDestinationRegistry.LegacyDestination(item.Codigo), model.Destino, StringComparison.Ordinal))
            ModelState.AddModelError(nameof(model.Destino), "El destino de un menú creado no se cambia para preservar sus permisos. Crea un menú nuevo para otro destino.");

        var rows = await service.ListarAsync();
        var byId = rows.ToDictionary(menu => menu.IdMenu);
        var parent = model.IdMenuPadre is { } parentId ? rows.FirstOrDefault(menu => menu.IdMenu == parentId) : null;
        if (model.IdMenuPadre is not null && (parent is null || !parent.Activo || parent.IdMenu == model.IdMenu
            || !HasActiveAncestors(parent, byId)))
            ModelState.AddModelError(nameof(model.IdMenuPadre), "Selecciona un menú superior activo distinto del actual.");
        if (model.IdMenu is { } currentId && model.IdMenuPadre is { } currentParent && IsDescendant(currentParent, currentId, rows))
            ModelState.AddModelError(nameof(model.IdMenuPadre), "No puedes mover un menú dentro de sí mismo o de uno de sus descendientes.");
        if (parent is not null && item is not null && item.Activo && !parent.Activo)
            ModelState.AddModelError(nameof(model.IdMenuPadre), "Un menú activo debe tener un superior activo.");

        var newDepth = parent is null ? 0 : DepthOf(parent, byId) + 1;
        var subtreeDepth = item is null ? 0 : MaxDescendantDepth(item.IdMenu, rows);
        if (newDepth + subtreeDepth > MaxDepth)
            ModelState.AddModelError(nameof(model.IdMenuPadre), "La navegación admite tres niveles como máximo.");

        await LoadParentsAsync(model.IdMenu);
        ViewBag.Destinations = MenuDestinationRegistry.All;
        if (!ModelState.IsValid) return View("Editar", model);

        item = await service.GuardarAsync(item, model, User);
        if (transaction is not null) await transaction.CommitAsync();
        TempData["Success"] = item.Destino is null
            ? $"Se guardó el agrupador «{item.Nombre}». Sus permisos se conceden en forma independiente a sus hijos."
            : $"Se guardó «{item.Nombre}». Asigna su acceso desde Permisos por rol.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        await using var transaction = await service.IniciarTransaccionAsync();
        var item = await service.ObtenerAsync(id, tracking: true);
        if (item is null) return NotFound();
        if (item.Activo && await HasActiveDescendantsAsync(id))
        {
            TempData["Error"] = "Antes de desactivar este menú, mueve o desactiva todos sus descendientes.";
            return RedirectToAction(nameof(Index));
        }
        if (!item.Activo && item.IdMenuPadre is { } parentId
            && !await HasActiveParentChainAsync(parentId))
        {
            TempData["Error"] = "Activa primero el menú superior.";
            return RedirectToAction(nameof(Index));
        }

        await service.CambiarEstadoAsync(item, User);
        if (transaction is not null) await transaction.CommitAsync();
        TempData["Success"] = item.Activo ? "Menú activado." : "Menú desactivado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadParentsAsync(int? excludeId = null)
    {
        ViewBag.Destinations = MenuDestinationRegistry.All;
        var rows = await service.ListarAsync();
        var byId = rows.ToDictionary(menu => menu.IdMenu);
        var parents = new List<MenuParentOptionViewModel>();
        var visited = new HashSet<int>();
        void Visit(int? parentId, int depth)
        {
            foreach (var menu in rows.Where(item => item.IdMenuPadre == parentId).OrderBy(item => item.Orden).ThenBy(item => item.IdMenu))
            {
                if (!visited.Add(menu.IdMenu) || !menu.Activo || !HasActiveAncestors(menu, byId)) continue;
                if (excludeId == menu.IdMenu || excludeId is { } excluded && IsDescendant(menu.IdMenu, excluded, rows)) continue;
                parents.Add(new MenuParentOptionViewModel(menu.IdMenu, menu.Nombre, depth));
                Visit(menu.IdMenu, depth + 1);
            }
        }
        Visit(null, 0);
        ViewBag.Parents = parents;
    }

    private async Task<bool> HasActiveParentChainAsync(int parentId)
    {
        var rows = await service.ListarAsync();
        var byId = rows.ToDictionary(menu => menu.IdMenu);
        return byId.TryGetValue(parentId, out var parent) && parent.Activo && HasActiveAncestors(parent, byId);
    }

    private static bool HasActiveAncestors(MenuSistema item, IReadOnlyDictionary<int, MenuSistema> byId)
    {
        var visited = new HashSet<int> { item.IdMenu };
        var parentId = item.IdMenuPadre;
        while (parentId is { } id)
        {
            if (!visited.Add(id) || !byId.TryGetValue(id, out var parent) || !parent.Activo) return false;
            parentId = parent.IdMenuPadre;
        }
        return true;
    }

    private async Task<bool> HasActiveDescendantsAsync(int id)
    {
        var rows = await service.ListarAsync();
        return rows.Any(menu => menu.Activo && menu.IdMenu != id && IsDescendant(menu.IdMenu, id, rows));
    }

    private static bool IsDescendant(int candidateId, int ancestorId, IReadOnlyCollection<MenuSistema> rows)
    {
        var byId = rows.ToDictionary(menu => menu.IdMenu);
        var seen = new HashSet<int>();
        var cursor = byId.GetValueOrDefault(candidateId);
        while (cursor?.IdMenuPadre is { } parentId && seen.Add(parentId))
        {
            if (parentId == ancestorId) return true;
            cursor = byId.GetValueOrDefault(parentId);
        }
        return false;
    }

    private static int DepthOf(MenuSistema item, IReadOnlyDictionary<int, MenuSistema> byId)
    {
        var depth = 0;
        var cursor = item;
        var seen = new HashSet<int> { item.IdMenu };
        while (cursor.IdMenuPadre is { } parentId && byId.TryGetValue(parentId, out cursor!) && seen.Add(parentId)) depth++;
        return depth;
    }

    private static int MaxDescendantDepth(int id, IReadOnlyCollection<MenuSistema> rows)
    {
        var max = 0;
        var stack = new Stack<(int Id, int Depth)>();
        stack.Push((id, 0));
        var visited = new HashSet<int>();
        while (stack.TryPop(out var current) && visited.Add(current.Id))
        {
            max = Math.Max(max, current.Depth);
            foreach (var child in rows.Where(item => item.IdMenuPadre == current.Id)) stack.Push((child.IdMenu, current.Depth + 1));
        }
        return max;
    }
}
