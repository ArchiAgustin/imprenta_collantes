using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;

namespace ProyectoFinal.Services;

public sealed class AuditoriaConsultaService(ApplicationDbContext db) : IAuditoriaConsultaService
{
    public async Task<AuditoriaListado> ListarAsync(string? tipoEntidad, int? idEntidad)
    {
        var query = db.AuditoriaOperacion.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tipoEntidad)) query = query.Where(item => item.TipoEntidad == tipoEntidad);
        if (idEntidad is not null) query = query.Where(item => item.IdEntidad == idEntidad);
        var entries = await query.OrderByDescending(item => item.FechaUtc).Take(500).ToListAsync();
        var users = await db.Users.AsNoTracking()
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id);
        return new(entries, users);
    }
}
