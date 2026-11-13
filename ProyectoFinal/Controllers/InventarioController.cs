using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class InventarioController(IInventarioConsultaService consulta, InventarioService inventario) : Controller
{
    public async Task<IActionResult> Index() => View(await consulta.ObtenerInicioAsync());

    [HttpGet]
    public async Task<IActionResult> BuscarProductos(string? termino, int almacenId)
    {
        if (!await consulta.AlmacenActivoAsync(almacenId)) return NotFound();
        var search = termino?.Trim();
        return Json(string.IsNullOrWhiteSpace(search) || search.Length < 2
            ? Array.Empty<ProductoBusquedaInventario>()
            : await consulta.BuscarProductosAsync(search, almacenId));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(NuevoInventarioViewModel model)
    {
        if (!ModelState.IsValid || model.Fecha is null)
        {
            TempData["Error"] = "Revisa la fecha, el almacén y las cantidades de los productos.";
            return RedirectToAction(nameof(Index));
        }
        var result = await inventario.CrearInventarioAsync(model.IdAlmacen, model.Fecha.Value,
            model.Detalles.Select(item => new InventarioLineaInput(item.IdProducto, item.CantidadBase)).ToList(),
            User.FindFirstValue(ClaimTypes.NameIdentifier), User);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return result.Exito && result.IdInventario is { } id
            ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var record = await consulta.ObtenerDetalleAsync(id);
        return record is null ? NotFound() : View(record);
    }

    public async Task<IActionResult> Kardex(int? almacenId)
    {
        var model = await consulta.ObtenerKardexAsync(almacenId);
        return model is null ? NotFound() : View(model);
    }
}
