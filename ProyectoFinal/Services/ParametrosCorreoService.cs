using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class ParametrosCorreoService(ApplicationDbContext db, IDataProtectionProvider protectionProvider)
    : IParametrosCorreoService
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("ProyectoFinal.SmtpSettings.v1");

    public async Task<ParametrosCorreoViewModel> ObtenerAsync()
    {
        var item = await db.ConfiguracionCorreo.AsNoTracking().SingleOrDefaultAsync(row => row.Id == 1);
        return item is null ? new ParametrosCorreoViewModel() : new ParametrosCorreoViewModel
        {
            Servidor = item.Servidor, Puerto = item.Puerto, CorreoRemitente = item.CorreoRemitente,
            NombreRemitente = item.NombreRemitente, Usuario = item.Usuario, UsarTls = item.UsarTls,
            TieneContrasenaGuardada = !string.IsNullOrEmpty(item.ContrasenaProtegida)
        };
    }

    public async Task<bool> TieneContrasenaAsync() => await db.ConfiguracionCorreo.AsNoTracking()
        .AnyAsync(item => item.Id == 1 && item.ContrasenaProtegida != null);

    public async Task GuardarAsync(ParametrosCorreoViewModel model)
    {
        var item = await db.ConfiguracionCorreo.SingleOrDefaultAsync(row => row.Id == 1);
        item ??= new ConfiguracionCorreo { Id = 1 };
        item.Servidor = model.Servidor;
        item.Puerto = model.Puerto;
        item.CorreoRemitente = model.CorreoRemitente;
        item.NombreRemitente = model.NombreRemitente;
        item.Usuario = model.Usuario;
        item.UsarTls = model.UsarTls;
        item.ActualizadoUtc = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(model.Contrasena)) item.ContrasenaProtegida = _protector.Protect(model.Contrasena);
        if (string.IsNullOrWhiteSpace(model.Usuario)) item.ContrasenaProtegida = null;
        if (db.Entry(item).State == EntityState.Detached) db.ConfiguracionCorreo.Add(item);
        await db.SaveChangesAsync();
    }
}
