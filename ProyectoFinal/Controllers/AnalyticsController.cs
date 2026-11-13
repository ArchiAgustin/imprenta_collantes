using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AnalyticsController(IAnalyticsService analytics, AnalyticsExportService exports) : Controller
{
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(DateOnly? desde, DateOnly? hasta, int? umbralStock)
    {
        TryGetFilters(desde, hasta, umbralStock, out var inicio, out var fin, out var umbral);

        if (!ModelState.IsValid)
        {
            ViewData["FiltroInvalido"] = true;
            return View(new AnalyticsDashboardViewModel { Desde = inicio, Hasta = fin, UmbralStock = umbral });
        }

        return View(await analytics.ObtenerAsync(inicio, fin, umbral));
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ExportarExcel(DateOnly? desde, DateOnly? hasta, int? umbralStock)
    {
        if (!TryGetFilters(desde, hasta, umbralStock, out var inicio, out var fin, out var umbral))
            return BadRequest(ModelState);
        var bytes = await exports.ExcelAsync(inicio, fin, umbral);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Analitica-{inicio:yyyyMMdd}-{fin:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ExportarPdf(DateOnly? desde, DateOnly? hasta, int? umbralStock)
    {
        if (!TryGetFilters(desde, hasta, umbralStock, out var inicio, out var fin, out var umbral))
            return BadRequest(ModelState);
        var bytes = await exports.PdfAsync(inicio, fin, umbral);
        return File(bytes, "application/pdf", $"Analitica-{inicio:yyyyMMdd}-{fin:yyyyMMdd}.pdf");
    }

    private bool TryGetFilters(DateOnly? desde, DateOnly? hasta, int? umbralStock,
        out DateOnly inicio, out DateOnly fin, out int umbral)
    {
        var hoy = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).DateTime);
        inicio = desde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        fin = hasta ?? hoy;
        umbral = umbralStock ?? 5;
        if (inicio.Year < 2000 || fin < inicio || fin.DayNumber - inicio.DayNumber > 365 || fin >= DateOnly.MaxValue)
            ModelState.AddModelError(string.Empty, "Selecciona un periodo válido de hasta 366 días (desde el año 2000).");
        if (umbral is < 0 or > 1_000_000)
            ModelState.AddModelError(string.Empty, "El umbral de stock debe estar entre 0 y 1 000 000.");
        return ModelState.IsValid;
    }
}
