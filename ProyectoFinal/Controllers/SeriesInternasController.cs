using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class SeriesInternasController(ApplicationDbContext db, SerieDocumentoInternoService service) : Controller
{
    public async Task<IActionResult> Index() => View(await db.SeriesDocumentoInterno.AsNoTracking()
        .OrderBy(item => item.TipoDocumento).ThenByDescending(item => item.Activa).ThenBy(item => item.Serie).ToListAsync());

    public IActionResult Create() => View("Editar", new SerieDocumentoInterno
    {
        TipoDocumento = SerieDocumentoInternoService.Cotizacion,
        SiguienteCorrelativo = 1, Digitos = 6, Activa = true
    });

    public async Task<IActionResult> Edit(int id)
    {
        var config = await db.SeriesDocumentoInterno.AsNoTracking().FirstOrDefaultAsync(item => item.IdSerieDocumentoInterno == id);
        return config is null ? NotFound() : View("Editar", config);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int? id, [Bind("TipoDocumento,Serie,SiguienteCorrelativo,Digitos,Activa")] SerieDocumentoInterno model)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Revisa la serie, los dígitos y el siguiente número.");
            model.IdSerieDocumentoInterno = id ?? 0;
            return View("Editar", model);
        }
        var result = await service.GuardarAsync(id, model.TipoDocumento, model.Serie, model.SiguienteCorrelativo,
            model.Digitos, model.Activa, User);
        if (!result.Exito)
        {
            ModelState.AddModelError(string.Empty, result.Mensaje);
            model.IdSerieDocumentoInterno = id ?? 0;
            return View("Editar", model);
        }
        TempData["Success"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
