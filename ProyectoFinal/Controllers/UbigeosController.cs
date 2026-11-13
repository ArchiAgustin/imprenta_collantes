using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public sealed class UbigeosController(IUbigeoConsultaService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Hijos(string parent)
    {
        if (parent.Length != 6 || !parent.All(char.IsAsciiDigit)) return BadRequest();
        var items = await service.HijosAsync(parent);
        if (items is null) return NotFound();
        return Json(items);
    }
}
