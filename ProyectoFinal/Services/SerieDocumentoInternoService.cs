using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record NumeroDocumentoInterno(string Serie, int Correlativo, int Digitos);
public sealed record ResultadoSerie(bool Exito, string Mensaje);

public sealed class SerieDocumentoInternoService(ApplicationDbContext db)
{
    public const string Cotizacion = "Cotizacion";
    public const string Venta = "Venta";

    // Call inside the commercial document transaction. The lock is owned by that
    // transaction, so rollback restores the counter together with the document.
    private async Task BloquearAsync(string tipo)
    {
        if (!db.Database.IsRelational()) return;
        var resource = "ProyectoFinal.Series." + tipo;
        var results = await db.Database.SqlQuery<int>($@"
DECLARE @result int;
EXEC @result = sp_getapplock @Resource = {resource},
    @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
SELECT @result AS [Value];").ToListAsync();
        if (results.Single() < 0) throw new TimeoutException("La numeración está ocupada. Reintenta esta operación.");
    }

    public async Task<NumeroDocumentoInterno> AsignarAsync(string tipo)
    {
        ValidarTipo(tipo);
        await BloquearAsync(tipo);
        var serie = await db.SeriesDocumentoInterno.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TipoDocumento == tipo && item.Activa);
        if (serie is null && !db.Database.IsRelational())
        {
            // EF InMemory test installations have no migrations to seed the catalog.
            serie = new SerieDocumentoInterno
            {
                TipoDocumento = tipo, Serie = tipo == Cotizacion ? "C001" : "V001",
                SiguienteCorrelativo = 4532, Digitos = 6, Activa = true
            };
            db.SeriesDocumentoInterno.Add(serie);
            await db.SaveChangesAsync();
        }
        if (serie is null) throw new InvalidOperationException($"No hay una serie interna activa para {tipo}. Configúrala antes de emitir.");
        if (serie.SiguienteCorrelativo == int.MaxValue)
            throw new InvalidOperationException("La serie alcanzó el máximo de correlativos. Activa otra serie.");
        var correlativo = serie.SiguienteCorrelativo;

        if (db.Database.IsRelational())
        {
            var updated = await db.SeriesDocumentoInterno
                .Where(item => item.IdSerieDocumentoInterno == serie.IdSerieDocumentoInterno
                    && item.Activa && item.SiguienteCorrelativo == serie.SiguienteCorrelativo)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.SiguienteCorrelativo, item => item.SiguienteCorrelativo + 1));
            if (updated != 1) throw new DbUpdateConcurrencyException("La configuración cambió durante la emisión. Reintenta.");
        }
        else
        {
            var tracked = await db.SeriesDocumentoInterno.FindAsync(serie.IdSerieDocumentoInterno);
            tracked!.SiguienteCorrelativo++;
        }
        return new NumeroDocumentoInterno(serie.Serie, correlativo, serie.Digitos);
    }

    public async Task<ResultadoSerie> GuardarAsync(int? id, string tipo, string codigo, int siguiente, int digitos, bool activar, System.Security.Claims.ClaimsPrincipal user)
    {
        if (tipo is not (Cotizacion or Venta) || codigo is null || !Regex.IsMatch(codigo, "^[A-Z0-9]{1,8}$")
            || siguiente < 1 || siguiente == int.MaxValue || digitos is < 4 or > 10)
            return new(false, "Usa una serie de 1 a 8 letras mayúsculas o números, correlativo positivo y de 4 a 10 dígitos.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        await BloquearAsync(tipo);

        var config = id is null ? null : await db.SeriesDocumentoInterno.FindAsync(id.Value);
        if (id is not null && (config is null || config.TipoDocumento != tipo))
            return new(false, "No existe esa configuración de serie.");
        if (await db.SeriesDocumentoInterno.AnyAsync(item => item.TipoDocumento == tipo && item.Serie == codigo && item.IdSerieDocumentoInterno != id))
            return new(false, "Esta serie ya está configurada para ese tipo de documento.");
        var maxUsado = tipo == Cotizacion
            ? await db.Cotizacion.Where(item => item.SerieInterna == codigo).MaxAsync(item => (int?)item.CorrelativoInterno)
            : await db.Venta.Where(item => item.SerieInterna == codigo).MaxAsync(item => (int?)item.CorrelativoInterno);
        if (maxUsado is { } max && siguiente <= max)
            return new(false, $"El siguiente correlativo debe superar el último número ya emitido de esta serie ({max}).");
        if (config is not null && config.Serie == codigo && siguiente < config.SiguienteCorrelativo)
            return new(false, "No se puede retroceder el próximo correlativo de una serie; conservar su avance evita reutilizar números.");

        if (activar)
        {
            if (db.Database.IsRelational())
                await db.SeriesDocumentoInterno.Where(item => item.TipoDocumento == tipo && item.Activa)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Activa, false));
            else
                foreach (var item in await db.SeriesDocumentoInterno.Where(item => item.TipoDocumento == tipo && item.Activa).ToListAsync()) item.Activa = false;
        }
        config ??= new SerieDocumentoInterno { TipoDocumento = tipo };
        config.Serie = codigo;
        config.Digitos = digitos;
        config.SiguienteCorrelativo = siguiente;
        if (activar) config.Activa = true;
        if (activar && id is not null && db.Database.IsRelational())
            db.Entry(config).Property(item => item.Activa).IsModified = true;
        if (id is null) db.SeriesDocumentoInterno.Add(config);
        await db.SaveChangesAsync();
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(user, "SerieDocumentoInterno", config.IdSerieDocumentoInterno,
            id is null ? "Serie creada" : "Serie configurada", null, codigo,
            $"Tipo {tipo}; próximo {siguiente}; dígitos {digitos}; activa {config.Activa}."));
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, $"Serie {codigo} guardada. Próximo número: {codigo}-{siguiente.ToString($"D{digitos}")}.");
    }

    private static void ValidarTipo(string tipo)
    {
        if (tipo is not (Cotizacion or Venta)) throw new ArgumentException("Tipo de documento no permitido.", nameof(tipo));
    }
}
