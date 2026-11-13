using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public class TallerController(ITallerConsultaService consulta, IVentaService ventaService) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(await consulta.ListarAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await consulta.ObtenerAsync(id);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarEstado(int id, string estado)
    {
        var result = await ventaService.AvanzarPedidoAsync(id, estado, User);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(nameof(Details), new { id });
    }
}
