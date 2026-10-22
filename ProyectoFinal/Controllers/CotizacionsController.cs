using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public class CotizacionsController : Controller
{
    private const int MaxLineas = 100;
    private const decimal MaximoTotal = 99_999_999.99m;
    private readonly ApplicationDbContext _context;

    public CotizacionsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var cotizaciones = await _context.Cotizacion
            .AsNoTracking()
            .Include(cotizacion => cotizacion.IdClienteNavigation)
            .Include(cotizacion => cotizacion.IdUsuarioNavigation)
            .Include(cotizacion => cotizacion.DetalleCotizacions)
            .OrderByDescending(cotizacion => cotizacion.FechaCreacion)
            .ThenByDescending(cotizacion => cotizacion.IdCotizacion)
            .ToListAsync();

        return View(cotizaciones);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();

        var cotizacion = await GetCotizacionWithLinesAsync(id.Value);
        return cotizacion is null ? NotFound() : View(cotizacion);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CotizacionFormViewModel
        {
            FechaEmision = TodayUtc(),
            Usuario = User.Identity?.Name ?? string.Empty
        };
        await LoadFormResourcesAsync(model);
        model.RecalcularTotales();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CotizacionFormViewModel model)
    {
        model.FechaEmision = TodayUtc();
        model.Usuario = User.Identity?.Name ?? string.Empty;
        model.Lineas ??= new List<CotizacionLineaInputViewModel>();
        await ValidateFormAsync(model);
        model.RecalcularTotales();
        ValidateTotal(model);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadFormResourcesAsync(model);
            return View(model);
        }

        var cotizacion = new Cotizacion
        {
            IdCliente = model.ClienteId,
            IdUsuario = userId,
            FechaCreacion = TodayUtc(),
            FechaVencimiento = model.FechaVencimiento,
            DetalleCotizacion = model.Descripcion?.Trim(),
            Total = model.Totales.Total
        };
        foreach (var linea in model.Lineas)
            cotizacion.DetalleCotizacions.Add(ToEntity(linea));

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        _context.Cotizacion.Add(cotizacion);
        await _context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        TempData["Success"] = $"Se guardó la cotización #{cotizacion.IdCotizacion} con sus líneas e importes calculados.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();

        var cotizacion = await GetCotizacionWithLinesAsync(id.Value);
        if (cotizacion is null) return NotFound();

        var model = new CotizacionFormViewModel
        {
            IdCotizacion = cotizacion.IdCotizacion,
            ClienteId = cotizacion.IdCliente,
            FechaVencimiento = cotizacion.FechaVencimiento,
            Descripcion = cotizacion.DetalleCotizacion,
            FechaEmision = cotizacion.FechaCreacion ?? TodayUtc(),
            Usuario = cotizacion.IdUsuarioNavigation?.Email ?? "Usuario no disponible",
            Lineas = cotizacion.DetalleCotizacions
                .OrderBy(linea => linea.IdDetalleCotizacion)
                .Select(linea => new CotizacionLineaInputViewModel
                {
                    ProductoId = linea.IdProducto,
                    Cantidad = linea.Cantidad ?? 1,
                    PrecioUnitario = linea.PrecioUnitario ?? 0
                })
                .ToList()
        };
        await LoadFormResourcesAsync(model);
        model.RecalcularTotales();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CotizacionFormViewModel model)
    {
        if (model.IdCotizacion is not null && model.IdCotizacion != id) return NotFound();

        var cotizacion = await _context.Cotizacion
            .Include(item => item.DetalleCotizacions)
            .Include(item => item.IdUsuarioNavigation)
            .FirstOrDefaultAsync(item => item.IdCotizacion == id);
        if (cotizacion is null) return NotFound();

        model.IdCotizacion = id;
        model.FechaEmision = cotizacion.FechaCreacion ?? TodayUtc();
        model.Usuario = cotizacion.IdUsuarioNavigation?.Email ?? "Usuario no disponible";
        model.Lineas ??= new List<CotizacionLineaInputViewModel>();
        await ValidateFormAsync(model);
        model.RecalcularTotales();
        ValidateTotal(model);

        if (!ModelState.IsValid)
        {
            await LoadFormResourcesAsync(model);
            return View(model);
        }

        cotizacion.IdCliente = model.ClienteId;
        cotizacion.FechaVencimiento = model.FechaVencimiento;
        cotizacion.DetalleCotizacion = model.Descripcion?.Trim();
        cotizacion.Total = model.Totales.Total;

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        _context.DetalleCotizacion.RemoveRange(cotizacion.DetalleCotizacions);
        cotizacion.DetalleCotizacions.Clear();
        foreach (var linea in model.Lineas)
            cotizacion.DetalleCotizacions.Add(ToEntity(linea));

        await _context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        TempData["Success"] = $"Se actualizaron la cotización #{id}, sus líneas y sus importes.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();

        var cotizacion = await GetCotizacionWithLinesAsync(id.Value);
        return cotizacion is null ? NotFound() : View(cotizacion);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var cotizacion = await GetCotizacionWithLinesAsync(id);
        if (cotizacion is null) return NotFound();

        if (await _context.Set<Pedido>().AnyAsync(pedido => pedido.IdCotizacion == id))
        {
            ModelState.AddModelError(string.Empty, "No se puede eliminar una cotización ya vinculada a un pedido.");
            return View("Delete", cotizacion);
        }

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        _context.DetalleCotizacion.RemoveRange(cotizacion.DetalleCotizacions);
        _context.Cotizacion.Remove(cotizacion);
        await _context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        TempData["Success"] = $"Se eliminó la cotización #{id} y sus líneas.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateFormAsync(CotizacionFormViewModel model)
    {
        if (model.ClienteId is null || !await _context.Cliente.AnyAsync(cliente => cliente.IdCliente == model.ClienteId))
            ModelState.AddModelError(nameof(model.ClienteId), "Selecciona un cliente registrado.");

        if (model.Lineas.Count == 0)
            ModelState.AddModelError(nameof(model.Lineas), "Agrega al menos un producto a la cotización.");
        else if (model.Lineas.Count > MaxLineas)
            ModelState.AddModelError(nameof(model.Lineas), $"Una cotización admite hasta {MaxLineas} líneas.");

        for (var index = 0; index < model.Lineas.Count; index++)
        {
            var productId = model.Lineas[index].ProductoId;
            if (productId is null || !await _context.Producto.AnyAsync(producto => producto.IdProducto == productId))
                ModelState.AddModelError($"{nameof(model.Lineas)}[{index}].{nameof(CotizacionLineaInputViewModel.ProductoId)}", "El producto seleccionado ya no existe.");
        }
    }

    private void ValidateTotal(CotizacionFormViewModel model)
    {
        if (model.Totales.Total > MaximoTotal)
            ModelState.AddModelError(nameof(model.Lineas), "El total supera el máximo que admite la base de datos.");
    }

    private async Task LoadFormResourcesAsync(CotizacionFormViewModel model)
    {
        model.Clientes = await _context.Cliente
            .AsNoTracking()
            .OrderBy(cliente => cliente.Nombre)
            .ToListAsync();
        model.Productos = await _context.Producto
            .AsNoTracking()
            .OrderBy(producto => producto.Nombre)
            .ToListAsync();
    }

    private Task<Cotizacion?> GetCotizacionWithLinesAsync(int id) => _context.Cotizacion
        .AsNoTracking()
        .Include(cotizacion => cotizacion.IdClienteNavigation)
        .Include(cotizacion => cotizacion.IdUsuarioNavigation)
        .Include(cotizacion => cotizacion.DetalleCotizacions)
            .ThenInclude(linea => linea.IdProductoNavigation)
        .Include(cotizacion => cotizacion.Pedidos)
        .FirstOrDefaultAsync(cotizacion => cotizacion.IdCotizacion == id);

    private static DetalleCotizacion ToEntity(CotizacionLineaInputViewModel linea) => new()
    {
        IdProducto = linea.ProductoId,
        Cantidad = linea.Cantidad,
        PrecioUnitario = linea.PrecioUnitario
    };

    private static DateOnly TodayUtc() => DateOnly.FromDateTime(DateTime.UtcNow);
}
