using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public class VentasController(ApplicationDbContext context, VentaService ventaService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var sales = await context.Venta.AsNoTracking()
            .Include(item => item.Cliente)
            .Include(item => item.Pedido)
            .OrderByDescending(item => item.FechaVentaUtc)
            .ToListAsync();
        return View(sales);
    }

    public async Task<IActionResult> Details(int id)
    {
        var sale = await context.Venta.AsNoTracking()
            .Include(item => item.Cliente)
            .Include(item => item.Cotizacion)
            .Include(item => item.Pedido)
            .Include(item => item.Detalles)
            .FirstOrDefaultAsync(item => item.IdVenta == id);
        return sale is null ? NotFound() : View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> Imprimir(int id)
    {
        var sale = await context.Venta.AsNoTracking().Include(item => item.Detalles)
            .FirstOrDefaultAsync(item => item.IdVenta == id);
        return sale is null ? NotFound() : View(sale);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarDesdeCotizacion(int cotizacionId)
    {
        var result = await ventaService.ConfirmarCotizacionAsync(cotizacionId,
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
