using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class SeriesInternasController(ISerieDocumentoInternoService service) : Controller
{
    public async Task<IActionResult> Index() => View(await service.ListarAsync());

    public async Task<IActionResult> Create()
    {
        await CargarTiposComprobanteAsync();
        return View("Editar", new SerieDocumentoInterno
        {
            TipoDocumento = SerieDocumentoInternoService.Cotizacion,
            SiguienteCorrelativo = 1, Digitos = 6, Activa = true
        });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var config = await service.ObtenerAsync(id);
        if (config is null) return NotFound();
        await CargarTiposComprobanteAsync(config.IdTipoComprobanteSunat);
        return View("Editar", config);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int? id, [Bind("TipoDocumento,Serie,SiguienteCorrelativo,Digitos,Activa,IdTipoComprobanteSunat")] SerieDocumentoInterno model)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Revisa la serie, los dígitos y el siguiente número.");
            model.IdSerieDocumentoInterno = id ?? 0;
            await CargarTiposComprobanteAsync(model.IdTipoComprobanteSunat);
            return View("Editar", model);
        }
        var result = await service.GuardarAsync(id, model.TipoDocumento, model.Serie, model.SiguienteCorrelativo,
            model.Digitos, model.Activa, User, model.IdTipoComprobanteSunat);
        if (!result.Exito)
        {
            ModelState.AddModelError(string.Empty, result.Mensaje);
            model.IdSerieDocumentoInterno = id ?? 0;
            await CargarTiposComprobanteAsync(model.IdTipoComprobanteSunat);
            return View("Editar", model);
        }
        TempData["Success"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarTiposComprobanteAsync(int? seleccionado = null)
    {
        var types = await service.TiposComprobanteActivosAsync();
        ViewData["TiposComprobanteSunat"] = types.Select(item => new SelectListItem(
            $"{item.CodigoSunat.Trim()} · {item.Descripcion}",
            item.IdTipoDocumentoPago.ToString(),
            item.IdTipoDocumentoPago == seleccionado)).ToList();
    }
}
