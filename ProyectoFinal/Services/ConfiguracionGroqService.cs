using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class ConfiguracionGroqService(ApplicationDbContext db, IDataProtectionProvider provider, IConfiguration configuration)
    : IConfiguracionGroqService
{
    private readonly IDataProtector _protector = provider.CreateProtector("ProyectoFinal.GroqSettings.v1");

    public async Task<bool> TieneClaveAsync() => await db.Set<ConfiguracionGroq>().AsNoTracking().AnyAsync()
        || !string.IsNullOrWhiteSpace(configuration["Groq:ApiKey"]);

    public async Task GuardarClaveAsync(string apiKey)
    {
        var row = await db.Set<ConfiguracionGroq>().SingleOrDefaultAsync(item => item.Id == 1);
        row ??= new ConfiguracionGroq();
        row.ApiKeyProtegida = _protector.Protect(apiKey.Trim());
        row.ActualizadoUtc = DateTimeOffset.UtcNow;
        if (db.Entry(row).State == EntityState.Detached) db.Set<ConfiguracionGroq>().Add(row);
        await db.SaveChangesAsync();
    }

    public async Task<string?> ObtenerClaveAsync()
    {
        var protectedKey = await db.Set<ConfiguracionGroq>().AsNoTracking()
            .Where(item => item.Id == 1).Select(item => item.ApiKeyProtegida).SingleOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(protectedKey))
        {
            try { return _protector.Unprotect(protectedKey); }
            catch (System.Security.Cryptography.CryptographicException) { return null; }
        }
        return configuration["Groq:ApiKey"];
    }
}
