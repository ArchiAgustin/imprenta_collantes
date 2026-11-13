using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public class VentasController(IVentaService ventaService) : Controller
{
    public async Task<IActionResult> Index() => View(await ventaService.ListarAsync());

    public async Task<IActionResult> Details(int id)
    {
        var sale = await ventaService.ObtenerDetalleAsync(id);
        return sale is null ? NotFound() : View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> Imprimir(int id)
    {
        var sale = await ventaService.ObtenerParaImprimirAsync(id);
        return sale is null ? NotFound() : View(sale);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarDesdeCotizacion(int cotizacionId, int idAlmacen)
    {
        var result = await ventaService.ConfirmarCotizacionAsync(cotizacionId, idAlmacen,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty, User);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return result.Exito
            ? RedirectToAction(nameof(Details), new { id = result.IdVenta })
            : RedirectToAction("Details", "Cotizacions", new { id = cotizacionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var result = await ventaService.CancelarAsync(id, User);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(nameof(Details), new { id });
    }
}
