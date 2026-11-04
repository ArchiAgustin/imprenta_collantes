using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class InventarioController(ApplicationDbContext db, InventarioService inventario) : Controller
{
    public async Task<IActionResult> Index()
    {
        var almacen = await db.Almacenes.AsNoTracking().FirstOrDefaultAsync(item => item.Nombre == InventarioService.NombreAlmacen);
        if (almacen is null) return View(new InventarioDashboardViewModel());
        var model = new InventarioDashboardViewModel
        {
            Activo = almacen.FechaActivacionUtc is not null,
            ConteoEnCurso = almacen.ConteoEnCurso,
            FechaActivacionUtc = almacen.FechaActivacionUtc,
            CorteAbierto = await db.CortesInventario.AsNoTracking()
                .FirstOrDefaultAsync(item => item.IdAlmacen == almacen.IdAlmacen && item.Estado == "Borrador"),
            Cortes = await db.CortesInventario.AsNoTracking().Where(item => item.IdAlmacen == almacen.IdAlmacen)
                .OrderByDescending(item => item.FechaUtc).Take(10).ToListAsync(),
            Movimientos = almacen.FechaActivacionUtc is null ? new() : await db.Kardex.AsNoTracking()
                .Include(item => item.Producto).Where(item => item.IdAlmacen == almacen.IdAlmacen)
                .OrderByDescending(item => item.IdKardex).Take(30).ToListAsync()
        };
        if (model.Activo)
            model.Saldos = await db.SaldosAlmacen.AsNoTracking().Where(item => item.IdAlmacen == almacen.IdAlmacen)
                .OrderBy(item => item.Producto.Nombre)
                .Select(item => new InventarioSaldoViewModel(item.IdProducto, item.Producto.Nombre ?? "Artículo sin nombre", item.CantidadBase))
                .ToListAsync();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Iniciar()
    {
        var result = await inventario.IniciarConteoAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return result.Exito ? RedirectToAction(nameof(Conteo), new { id = result.IdCorte }) : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Conteo(int id)
    {
        var corte = await db.CortesInventario.AsNoTracking().Include(item => item.Detalles)
            .ThenInclude(item => item.Producto).FirstOrDefaultAsync(item => item.IdCorteInventario == id);
        if (corte is null) return NotFound();
        if (corte.Estado != "Borrador") return RedirectToAction(nameof(Index));
        return View(new ConteoViewModel
        {
            IdCorte = corte.IdCorteInventario,
            FechaUtc = corte.FechaUtc,
            Apertura = !await db.Almacenes.AnyAsync(item => item.IdAlmacen == corte.IdAlmacen && item.FechaActivacionUtc != null),
            Articulos = corte.Detalles.OrderBy(item => item.Producto.Nombre).Select(item => new ConteoArticuloViewModel
            {
                IdProducto = item.IdProducto, Nombre = item.Producto.Nombre ?? "Artículo sin nombre",
                CantidadSistemaBase = item.CantidadSistemaBase,
                CantidadContadaBase = item.ConteoRegistrado ? item.CantidadContadaBase : null
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int id, ConteoViewModel model)
    {
        if (id != model.IdCorte || !ModelState.IsValid)
        {
            TempData["Error"] = "Revisa las cantidades: no se guardó el conteo.";
            return RedirectToAction(nameof(Conteo), new { id });
        }
        var result = await inventario.GuardarConteoAsync(id, model.Articulos
            .Select(item => new ConteoLineaInput(item.IdProducto, item.CantidadContadaBase)).ToList());
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(nameof(Conteo), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var result = await inventario.FinalizarConteoAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), aprobar: true);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return result.Exito ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Conteo), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id)
    {
        var result = await inventario.FinalizarConteoAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), aprobar: false);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrada(EntradaInventarioViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Indica un artículo, cantidad positiva y motivo para la entrada.";
            return RedirectToAction(nameof(Index));
        }
        var result = await inventario.RegistrarEntradaAsync(model.IdProducto, model.CantidadBase, model.Motivo,
            User.FindFirstValue(ClaimTypes.NameIdentifier));
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
