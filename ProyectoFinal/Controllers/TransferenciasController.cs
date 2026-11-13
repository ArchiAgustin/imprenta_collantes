using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class TransferenciasController(
    ITransferenciaAlmacenService transferencias) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(await transferencias.ObtenerPanelAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(TransferenciaAlmacenViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Revisa almacenes, artículo, cantidad y motivo de la transferencia.";
            return RedirectToAction(nameof(Index));
        }
        var result = await transferencias.TransferirAsync(model.IdAlmacenOrigen, model.IdAlmacenDestino,
            model.IdProducto, model.CantidadBase, model.Motivo,
            User.FindFirstValue(ClaimTypes.NameIdentifier), User);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
