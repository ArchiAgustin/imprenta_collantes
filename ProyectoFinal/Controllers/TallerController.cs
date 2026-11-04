using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public class TallerController(ApplicationDbContext context, VentaService ventaService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var orders = await context.Pedido.AsNoTracking()
            .Include(item => item.IdVentaNavigation)
            .ThenInclude(sale => sale!.Cliente)
            .Include(item => item.DetallePedidos)
            .OrderBy(item => item.EstadoPedido == EstadoPedido.EnCola ? 0 : 1)
            .ThenBy(item => item.FechaPedido)
            .ThenBy(item => item.IdPedido)
            .ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await context.Pedido.AsNoTracking()
            .Include(item => item.IdVentaNavigation)
            .ThenInclude(sale => sale!.Cliente)
            .Include(item => item.DetallePedidos)
            .Include(item => item.IdClienteNavigation)
            .FirstOrDefaultAsync(item => item.IdPedido == id);
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
