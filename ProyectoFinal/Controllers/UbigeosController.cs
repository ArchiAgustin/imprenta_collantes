using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;

namespace ProyectoFinal.Controllers;

[Authorize]
public sealed class UbigeosController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Hijos(string parent)
    {
        if (parent.Length != 6 || !parent.All(char.IsAsciiDigit)) return BadRequest();
        if (!await db.Ubigeos.AnyAsync(item => item.Codigo == parent)) return NotFound();
        var items = await db.Ubigeos.AsNoTracking().Where(item => item.Parent == parent)
            .OrderBy(item => item.Descripcion)
            .Select(item => new { codigo = item.Codigo, nombre = item.Descripcion }).ToListAsync();
        return Json(items);
    }
}
