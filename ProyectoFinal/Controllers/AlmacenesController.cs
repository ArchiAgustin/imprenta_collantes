using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AlmacenesController(IAlmacenesAdminService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.ListarAsync());

    [HttpGet]
    public IActionResult Create() => View("Editar", new AlmacenFormViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var warehouse = await service.ObtenerAsync(id);
        return warehouse is null ? NotFound() : View("Editar", new AlmacenFormViewModel
        {
            IdAlmacen = warehouse.IdAlmacen, Nombre = warehouse.Nombre,
            Activo = warehouse.Activo, EsPredeterminado = warehouse.EsPredeterminado
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(AlmacenFormViewModel model)
    {
        model.Nombre = model.Nombre?.Trim() ?? string.Empty;
        var warehouse = model.IdAlmacen is { } id ? await service.ObtenerAsync(id) : null;
        if (model.IdAlmacen is not null && warehouse is null) return NotFound();
        if (await service.NombreExisteAsync(model.Nombre, model.IdAlmacen))
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe un almacén con ese nombre.");
        if (!ModelState.IsValid)
        {
            model.Activo = warehouse?.Activo ?? true;
            model.EsPredeterminado = warehouse?.EsPredeterminado ?? false;
            return View("Editar", model);
        }
        var result = await service.GuardarAsync(model, User);
        if (!result.Encontrado) return NotFound();
        if (!result.Exito)
        {
            ModelState.AddModelError(nameof(model.Nombre), result.Mensaje);
            return View("Editar", model);
        }
        TempData["Success"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var result = await service.CambiarEstadoAsync(id, User);
        if (!result.Encontrado) return NotFound();
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
