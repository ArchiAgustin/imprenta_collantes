using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AnalyticsController(AnalyticsService analytics) : Controller
{
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(DateOnly? desde, DateOnly? hasta, int? umbralStock)
    {
        var hoy = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).DateTime);
        var inicio = desde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var fin = hasta ?? hoy;
        var umbral = umbralStock ?? 5;

        if (inicio.Year < 2000 || fin < inicio || fin.DayNumber - inicio.DayNumber > 365 || fin >= DateOnly.MaxValue)
            ModelState.AddModelError(string.Empty, "Selecciona un periodo válido de hasta 366 días (desde el año 2000).");
        if (umbral is < 0 or > 1_000_000)
            ModelState.AddModelError(string.Empty, "El umbral de stock debe estar entre 0 y 1 000 000.");

        if (!ModelState.IsValid)
        {
            ViewData["FiltroInvalido"] = true;
            return View(new AnalyticsDashboardViewModel { Desde = inicio, Hasta = fin, UmbralStock = umbral });
        }

        return View(await analytics.ObtenerAsync(inicio, fin, umbral));
    }
}
