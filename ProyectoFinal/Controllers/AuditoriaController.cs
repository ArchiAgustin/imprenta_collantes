using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public class AuditoriaController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index(string? tipoEntidad, int? idEntidad)
    {
        var query = context.AuditoriaOperacion.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tipoEntidad)) query = query.Where(item => item.TipoEntidad == tipoEntidad);
        if (idEntidad is not null) query = query.Where(item => item.IdEntidad == idEntidad);
        var entries = await query.OrderByDescending(item => item.FechaUtc).Take(500).ToListAsync();
        var users = await context.Users.AsNoTracking().ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id);
        ViewBag.UserEmails = users;
        ViewBag.TipoEntidad = tipoEntidad;
        ViewBag.IdEntidad = idEntidad;
        return View(entries);
    }
}
