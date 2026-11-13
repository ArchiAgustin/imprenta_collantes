using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class TramosDescuentoController(ITramosDescuentoService service) : Controller
{
    public async Task<IActionResult> Index(int productoId)
    {
        var producto = await service.ObtenerProductoAsync(productoId);
        if (producto is null) return NotFound();
        ViewBag.Producto = producto;
        return View(await service.ListarAsync(productoId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Agregar(int productoId, int cantidadMinima, decimal descuentoPorcentaje)
    {
        var agregado = await service.AgregarAsync(productoId, cantidadMinima, descuentoPorcentaje);
        TempData[agregado ? "Success" : "Error"] = agregado
            ? "Tramo agregado."
            : "Revisa el producto, la cantidad mínima única y el descuento entre 0 y 99,99 % con dos decimales.";
        return RedirectToAction(nameof(Index), new { productoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int productoId, int id)
    {
        if (!await service.EliminarAsync(productoId, id)) return NotFound();
        return RedirectToAction(nameof(Index), new { productoId });
    }
}
