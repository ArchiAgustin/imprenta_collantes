using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public class CotizacionsController : Controller
{
    private const decimal MaximoTotal = 99_999_999.99m;
    private readonly ICotizacionService _cotizaciones;
    private readonly CotizacionExpirationService _expiration;
    private readonly IMenuAccessService _menuAccess;
    private readonly CotizacionPdfService _pdf;
    private readonly ICorreoComercialSender _correo;
    private readonly ILogger<CotizacionsController> _logger;
    private readonly SugerenciaPrecioService _precios;

    public CotizacionsController(ICotizacionService cotizaciones, CotizacionExpirationService expiration,
        IMenuAccessService menuAccess, CotizacionPdfService pdf, ICorreoComercialSender correo,
        ILogger<CotizacionsController> logger, SugerenciaPrecioService precios)
    {
        _cotizaciones = cotizaciones;
        _expiration = expiration;
        _menuAccess = menuAccess;
        _pdf = pdf;
        _correo = correo;
        _logger = logger;
        _precios = precios;
    }

    public async Task<IActionResult> Index()
    {
        await _expiration.ExpireDueAsync();
        return View(await _cotizaciones.ListarAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();

        await _expiration.ExpireDueAsync();
        var cotizacion = await GetCotizacionWithLinesAsync(id.Value);
        if (cotizacion is null) return NotFound();
        ViewData["AlmacenesVenta"] = await _cotizaciones.ObtenerAlmacenesVentaAsync();
        return View(cotizacion);
    }

    [HttpGet]
    public async Task<IActionResult> Imprimir(int id)
    {
        var cotizacion = await GetCotizacionWithLinesAsync(id);
        return cotizacion is null ? NotFound() : View(cotizacion);
    }

    [HttpGet]
    public async Task<IActionResult> DescargarPdf(int id)
    {
        var cotizacion = await GetCotizacionWithLinesAsync(id);
        if (cotizacion is null) return NotFound();
        var bytes = _pdf.Generar(cotizacion);
        return File(bytes, "application/pdf", $"Cotizacion-{cotizacion.NumeroInterno}.pdf");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("cotizacion-email")]
    public async Task<IActionResult> EnviarPorCorreo(int id)
    {
        if (!await _menuAccess.HasPermissionAsync(User, "cotizaciones", MenuPermissionKind.Create)) return Forbid();
        await _expiration.ExpireDueAsync();
        var cotizacion = await GetCotizacionWithLinesAsync(id);
        if (cotizacion is null) return NotFound();
        if (cotizacion.Estado != "Emitida" || cotizacion.FechaVencimiento is { } vencimiento && vencimiento < FechaComercial.HoyPeru())
        {
            TempData["Error"] = "Solo se puede enviar una cotización emitida y vigente.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var correo = cotizacion.IdClienteNavigation?.Correo;
        if (string.IsNullOrWhiteSpace(correo))
        {
            TempData["Error"] = "El cliente no tiene un correo registrado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var pdf = _pdf.Generar(cotizacion);
            var nombreArchivo = $"Cotizacion-{cotizacion.NumeroInterno}.pdf";
            await _correo.SendEmailWithAttachmentAsync(correo,
                $"Cotización {cotizacion.NumeroInterno} · Imprenta Collantes",
                $"<p>Adjuntamos la cotización comercial <strong>{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(cotizacion.NumeroInterno)}</strong>.</p><p>Este documento es una propuesta comercial; no es un comprobante de pago tributario.</p>",
                pdf, nombreArchivo, "application/pdf");
            await _cotizaciones.RegistrarEnvioPorCorreoAsync(id, User);
            TempData["Success"] = $"Se envió la cotización {cotizacion.NumeroInterno} a {correo}.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo enviar la cotización {QuoteId} por correo", id);
            TempData["Error"] = "No se pudo enviar la cotización. Revisa la configuración SMTP en Administración → Configuración → Parámetros.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Revalidar(int id)
    {
        if (!await _menuAccess.HasPermissionAsync(User, "cotizaciones", MenuPermissionKind.Create)) return Forbid();
        await _expiration.ExpireDueAsync();
        var original = await GetCotizacionWithLinesAsync(id);
        if (original is null) return NotFound();
        if (original.Estado != "Vencida" || await _cotizaciones.TieneVentaAsync(id))
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
                     IdPresentacionProducto = item.IdPresentacionProducto,
                     FactorPresentacion = item.FactorPresentacion,
                     PresentacionDescripcion = item.PresentacionDescripcion,
                     UnidadBaseDescripcion = item.UnidadBaseDescripcion,
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
    public async Task<IActionResult> RevisarPrecio(CotizacionFormViewModel model)
    {
        if (!await _menuAccess.HasPermissionAsync(User, "cotizaciones", MenuPermissionKind.Create)) return Forbid();
        model.Lineas ??= new List<CotizacionLineaInputViewModel>();
        model.FechaEmision = TodayUtc();
        model.FechaVencimiento ??= TodayUtc().AddDays(7);
        model.Usuario = User.Identity?.Name ?? string.Empty;
        await LoadFormResourcesAsync(model);
        model.RecalcularTotales();
        if (model.ClienteId is { } clienteId && model.Clientes.Any(item => item.IdCliente == clienteId)
            && model.Lineas.Count is > 0 and <= 100)
            model.ReferenciasPrecio = await _precios.ObtenerAsync(clienteId, model.Lineas);
        else
            ModelState.AddModelError(string.Empty, "Selecciona un cliente y al menos una línea para revisar precios.");
        return View("Create", model);
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        foreach (var error in await _cotizaciones.ValidarFormularioAsync(model))
            ModelState.AddModelError(error.Campo, error.Mensaje);
        model.RecalcularTotales();
        ValidateTotal(model);

        if (!ModelState.IsValid)
        {
            await LoadFormResourcesAsync(model);
            return View(model);
        }

        var result = await _cotizaciones.CrearAsync(model, userId, User);
        if (!result.Exito)
        {
            ModelState.AddModelError(string.Empty, result.Mensaje);
            await LoadFormResourcesAsync(model);
            return View(model);
        }
        TempData["Success"] = result.Mensaje;
        return RedirectToAction(nameof(Details), new { id = result.IdCotizacion });
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
                     IdPresentacionProducto = linea.IdPresentacionProducto,
                     FactorPresentacion = linea.FactorPresentacion,
                     PresentacionDescripcion = linea.PresentacionDescripcion,
                     UnidadBaseDescripcion = linea.UnidadBaseDescripcion,
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
        var cotizacion = await _cotizaciones.ObtenerParaEdicionAsync(id);
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
        foreach (var error in await _cotizaciones.ValidarFormularioAsync(model))
            ModelState.AddModelError(error.Campo, error.Mensaje);
        model.RecalcularTotales();
        ValidateTotal(model);

        if (!ModelState.IsValid)
        {
            await LoadFormResourcesAsync(model);
            return View(model);
        }

        var result = await _cotizaciones.ActualizarAsync(id, model, User);
        if (!result.Exito)
        {
            TempData["Error"] = result.Mensaje;
            return result.Estado == EstadoOperacionCotizacion.NoEncontrada
                ? NotFound()
                : RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = result.Mensaje;
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
        var result = await _cotizaciones.EliminarAsync(id, User);
        if (result.Estado == EstadoOperacionCotizacion.NoEncontrada) return NotFound();
        if (!result.Exito)
        {
            TempData["Error"] = result.Mensaje;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private void ValidateTotal(CotizacionFormViewModel model)
    {
        if (model.Totales.Total > MaximoTotal)
            ModelState.AddModelError(nameof(model.Lineas), "El total supera el máximo que admite la base de datos.");
    }

    private Task LoadFormResourcesAsync(CotizacionFormViewModel model) => _cotizaciones.CargarRecursosFormularioAsync(model);

    private Task<Cotizacion?> GetCotizacionWithLinesAsync(int id) => _cotizaciones.ObtenerConLineasAsync(id);

    private static DateOnly TodayUtc() => FechaComercial.HoyPeru();
}
