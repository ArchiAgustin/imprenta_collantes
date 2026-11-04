using System.Security.Claims;
using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public class CotizacionsController : Controller
{
    private const int MaxLineas = 100;
    private const decimal MaximoTotal = 99_999_999.99m;
    private readonly ApplicationDbContext _context;
    private readonly CotizacionExpirationService _expiration;
    private readonly IMenuAccessService _menuAccess;

    public CotizacionsController(ApplicationDbContext context, CotizacionExpirationService expiration, IMenuAccessService menuAccess)
    {
        _context = context;
        _expiration = expiration;
        _menuAccess = menuAccess;
    }

    public async Task<IActionResult> Index()
    {
        await _expiration.ExpireDueAsync();
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

        await _expiration.ExpireDueAsync();
        var cotizacion = await GetCotizacionWithLinesAsync(id.Value);
        return cotizacion is null ? NotFound() : View(cotizacion);
    }

    [HttpGet]
    public async Task<IActionResult> Imprimir(int id)
    {
        var cotizacion = await GetCotizacionWithLinesAsync(id);
        return cotizacion is null ? NotFound() : View(cotizacion);
    }

    [HttpGet]
    public async Task<IActionResult> Revalidar(int id)
    {
        if (!await _menuAccess.HasPermissionAsync(User, "cotizaciones", MenuPermissionKind.Create)) return Forbid();
        await _expiration.ExpireDueAsync();
        var original = await GetCotizacionWithLinesAsync(id);
        if (original is null) return NotFound();
        if (original.Estado != "Vencida" || await _context.Venta.AnyAsync(item => item.IdCotizacion == id))
        {
            TempData["Error"] = "Solo se revalidan cotizaciones vencidas sin venta asociada.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var model = new CotizacionFormViewModel
        {
            IdCotizacionOrigen = id, ClienteId = original.IdCliente,
            FechaEmision = TodayUtc(), FechaVencimiento = TodayUtc().AddDays(7),
            Usuario = User.Identity?.Name ?? string.Empty, Descripcion = original.DetalleCotizacion,
            Lineas = original.DetalleCotizacions.OrderBy(item => item.IdDetalleCotizacion)
                .Select(item => new CotizacionLineaInputViewModel
                {
                    ProductoId = item.IdProducto, Cantidad = item.Cantidad ?? 1,
                    PrecioUnitario = item.PrecioUnitario ?? 0
                }).ToList()
        };
        await LoadFormResourcesAsync(model);
        model.RecalcularTotales();
        return View("Create", model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CotizacionFormViewModel
        {
            FechaEmision = TodayUtc(),
            FechaVencimiento = TodayUtc().AddDays(7),
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
        // Also enforce the default for older clients that do not post a date.
        model.FechaVencimiento ??= TodayUtc().AddDays(7);
        if (model.FechaVencimiento < TodayUtc())
            ModelState.AddModelError(nameof(model.FechaVencimiento), "Indica una vigencia igual o posterior a hoy (Perú).");
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

        Cotizacion? original = null;
        if (model.IdCotizacionOrigen is { } origenId)
        {
            original = await _context.Cotizacion.AsNoTracking().FirstOrDefaultAsync(item => item.IdCotizacion == origenId);
            if (original is null || original.Estado != "Vencida" || await _context.Venta.AnyAsync(item => item.IdCotizacion == origenId))
            {
                ModelState.AddModelError(string.Empty, "La cotización de origen ya no está vencida o tiene una venta. Recarga y revisa su estado.");
                await LoadFormResourcesAsync(model);
                return View(model);
            }
        }

        var cotizacion = new Cotizacion
        {
            IdCliente = model.ClienteId,
            IdUsuario = userId,
            IdCotizacionOrigen = original?.IdCotizacion,
            FechaCreacion = TodayUtc(),
            FechaVencimiento = model.FechaVencimiento,
            DetalleCotizacion = model.Descripcion?.Trim(),
            Total = model.Totales.Total
        };
        var clienteEmision = await _context.Cliente.AsNoTracking().FirstAsync(item => item.IdCliente == model.ClienteId);
        cotizacion.ClienteNombreEmision = clienteEmision.Nombre;
        cotizacion.ClienteDocumentoEmision = clienteEmision.NumeroDocumento ?? clienteEmision.Dni;
        cotizacion.ClienteDireccionEmision = clienteEmision.Direccion;
        var idsProductos = model.Lineas.Select(line => line.ProductoId!.Value).Distinct().ToArray();
        var nombresProductos = await _context.Producto.AsNoTracking()
            .Where(item => idsProductos.Contains(item.IdProducto))
            .ToDictionaryAsync(item => item.IdProducto, item => item.Nombre);
        foreach (var linea in model.Lineas)
            cotizacion.DetalleCotizacions.Add(ToEntity(linea, nombresProductos[linea.ProductoId!.Value]));

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        var numero = await new SerieDocumentoInternoService(_context).AsignarAsync(SerieDocumentoInternoService.Cotizacion);
        cotizacion.SerieInterna = numero.Serie;
        cotizacion.CorrelativoInterno = numero.Correlativo;
        cotizacion.DigitosNumeroInterno = numero.Digitos;
        _context.Cotizacion.Add(cotizacion);
        await _context.SaveChangesAsync();
        _context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(User, "Cotizacion", cotizacion.IdCotizacion,
            original is null ? "Cotización emitida" : "Nueva cotización tras revalidación", null, cotizacion.Estado,
            original is null ? null : $"Origen vencido #{original.IdCotizacion}; revisión humana, original conservado."));
        await _context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        TempData["Success"] = $"Se emitió la cotización {cotizacion.NumeroInterno} con sus líneas e importes calculados.";
        return RedirectToAction(nameof(Details), new { id = cotizacion.IdCotizacion });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();

        await _expiration.ExpireDueAsync();
        var cotizacion = await GetCotizacionWithLinesAsync(id.Value);
        if (cotizacion is null) return NotFound();
        if (cotizacion.Estado != "Emitida")
        {
            TempData["Error"] = "Solo se pueden editar cotizaciones emitidas y vigentes.";
            return RedirectToAction(nameof(Details), new { id });
        }

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

        await _expiration.ExpireDueAsync();
        var cotizacion = await _context.Cotizacion
            .Include(item => item.DetalleCotizacions)
            .Include(item => item.IdUsuarioNavigation)
            .FirstOrDefaultAsync(item => item.IdCotizacion == id);
        if (cotizacion is null) return NotFound();
        if (cotizacion.Estado != "Emitida")
        {
            TempData["Error"] = "Solo se pueden editar cotizaciones emitidas y vigentes.";
            return RedirectToAction(nameof(Details), new { id });
        }

        model.IdCotizacion = id;
        model.FechaEmision = cotizacion.FechaCreacion ?? TodayUtc();
        model.Usuario = cotizacion.IdUsuarioNavigation?.Email ?? "Usuario no disponible";
        model.Lineas ??= new List<CotizacionLineaInputViewModel>();
        model.FechaVencimiento ??= cotizacion.FechaVencimiento ?? TodayUtc().AddDays(7);
        if (model.FechaVencimiento < TodayUtc())
            ModelState.AddModelError(nameof(model.FechaVencimiento), "Indica una vigencia igual o posterior a hoy (Perú).");
        await ValidateFormAsync(model);
        model.RecalcularTotales();
        ValidateTotal(model);

        if (!ModelState.IsValid)
        {
            await LoadFormResourcesAsync(model);
            return View(model);
        }

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;

        if (transaction is not null)
        {
            // Take an update lock before rewriting the lines. Confirmation cannot read
            // a mix of the old header and new detail, or edit a converted quote.
            var claimed = await _context.Cotizacion
                .Where(item => item.IdCotizacion == id && item.Estado == "Emitida"
                    && (item.FechaVencimiento == null || item.FechaVencimiento >= TodayUtc()))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Estado, "Emitida"));
            if (claimed != 1)
            {
                TempData["Error"] = "La cotización venció o fue convertida mientras la editabas. No se guardaron los cambios.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        cotizacion.IdCliente = model.ClienteId;
        var clienteActual = await _context.Cliente.AsNoTracking().FirstAsync(item => item.IdCliente == model.ClienteId);
        cotizacion.ClienteNombreEmision = clienteActual.Nombre;
        cotizacion.ClienteDocumentoEmision = clienteActual.NumeroDocumento ?? clienteActual.Dni;
        cotizacion.ClienteDireccionEmision = clienteActual.Direccion;
        cotizacion.FechaVencimiento = model.FechaVencimiento;
        cotizacion.DetalleCotizacion = model.Descripcion?.Trim();
        cotizacion.Total = model.Totales.Total;

        _context.DetalleCotizacion.RemoveRange(cotizacion.DetalleCotizacions);
        cotizacion.DetalleCotizacions.Clear();
        var idsActuales = model.Lineas.Select(line => line.ProductoId!.Value).Distinct().ToArray();
        var nombresActuales = await _context.Producto.AsNoTracking()
            .Where(item => idsActuales.Contains(item.IdProducto))
            .ToDictionaryAsync(item => item.IdProducto, item => item.Nombre);
        foreach (var linea in model.Lineas)
            cotizacion.DetalleCotizacions.Add(ToEntity(linea, nombresActuales[linea.ProductoId!.Value]));

        await _context.SaveChangesAsync();
        _context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(User, "Cotizacion", id,
            "Cotización actualizada", "Emitida", "Emitida"));
        await _context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        TempData["Success"] = $"Se actualizaron la cotización #{id}, sus líneas y sus importes.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();

        await _expiration.ExpireDueAsync();
        var cotizacion = await GetCotizacionWithLinesAsync(id.Value);
        if (cotizacion is null) return NotFound();
        if (cotizacion.Estado != "Emitida")
        {
            TempData["Error"] = "Solo se pueden eliminar cotizaciones emitidas que no se hayan convertido en venta.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(cotizacion);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _expiration.ExpireDueAsync();
        var cotizacion = await GetCotizacionWithLinesAsync(id);
        if (cotizacion is null) return NotFound();

        if (cotizacion.Estado != "Emitida")
        {
            TempData["Error"] = "Solo se pueden eliminar cotizaciones emitidas que no se hayan convertido en venta.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;

        if (transaction is not null)
        {
            var claimed = await _context.Cotizacion
                .Where(item => item.IdCotizacion == id && item.Estado == "Emitida"
                    && (item.FechaVencimiento == null || item.FechaVencimiento >= TodayUtc()))
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Estado, "Emitida"));
            if (claimed != 1)
            {
                TempData["Error"] = "La cotización venció o fue convertida mientras la eliminabas.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        if (await _context.Venta.AnyAsync(venta => venta.IdCotizacion == id)
            || await _context.Pedido.AnyAsync(pedido => pedido.IdCotizacion == id))
        {
            ModelState.AddModelError(string.Empty, "No se puede eliminar una cotización ya vinculada a un pedido.");
            return View("Delete", cotizacion);
        }

        _context.DetalleCotizacion.RemoveRange(cotizacion.DetalleCotizacions);
        _context.Cotizacion.Remove(cotizacion);
        _context.AuditoriaOperacion.Add(AuditoriaFactory.Crear(User, "Cotizacion", id,
            "Cotización eliminada", "Emitida", null));
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

        var requestedIds = model.Lineas.Where(line => line?.ProductoId is not null)
            .Select(line => line.ProductoId!.Value).Distinct().ToArray();
        var products = await _context.Producto.AsNoTracking()
            .Where(producto => requestedIds.Contains(producto.IdProducto))
            .Select(producto => new { producto.IdProducto, producto.CostoUnitarioSinIgv })
            .ToDictionaryAsync(producto => producto.IdProducto);

        for (var index = 0; index < model.Lineas.Count; index++)
        {
            var line = model.Lineas[index];
            if (line is null || line.ProductoId is not { } productId || !products.TryGetValue(productId, out var product))
            {
                ModelState.AddModelError($"{nameof(model.Lineas)}[{index}].{nameof(CotizacionLineaInputViewModel.ProductoId)}", "El producto seleccionado ya no existe.");
                continue;
            }
            var minPrice = SugerenciaPrecioService.CalcularMinimo(product.CostoUnitarioSinIgv);
            if (minPrice is { } floor && line.PrecioUnitario < floor)
                ModelState.AddModelError($"{nameof(model.Lineas)}[{index}].{nameof(CotizacionLineaInputViewModel.PrecioUnitario)}",
                    $"El precio unitario sin IGV debe ser al menos {floor.ToString("C2", CultureInfo.GetCultureInfo("es-PE"))} para conservar un margen mínimo del 10 %.");
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

    private static DetalleCotizacion ToEntity(CotizacionLineaInputViewModel linea, string? nombre) => new()
    {
        IdProducto = linea.ProductoId,
        ProductoNombreEmision = nombre,
        Cantidad = linea.Cantidad,
        PrecioUnitario = linea.PrecioUnitario
    };

    private static DateOnly TodayUtc() => FechaComercial.HoyPeru();
}
