using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public class AuditoriaController(IAuditoriaConsultaService service) : Controller
{
    public async Task<IActionResult> Index(string? tipoEntidad, int? idEntidad)
    {
        var listing = await service.ListarAsync(tipoEntidad, idEntidad);
        ViewBag.UserEmails = listing.Usuarios;
        ViewBag.TipoEntidad = tipoEntidad;
        ViewBag.IdEntidad = idEntidad;
        return View(listing.Registros);
    }
}
