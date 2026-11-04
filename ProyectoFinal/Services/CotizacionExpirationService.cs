using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class CotizacionExpirationService(ApplicationDbContext context)
{
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var today = FechaComercial.HoyPeru();
        var dueQuotes = await context.Cotizacion
            .Where(quote => quote.Estado == "Emitida"
                && quote.FechaVencimiento != null
                && quote.FechaVencimiento < today)
            .ToListAsync(cancellationToken);
        if (dueQuotes.Count == 0) return 0;

        foreach (var quote in dueQuotes)
        {
            quote.Estado = "Vencida";
            context.AuditoriaOperacion.Add(AuditoriaFactory.CrearSistema(
                "Cotizacion", quote.IdCotizacion, "Vencimiento automático", "Emitida", "Vencida"));
        }

        await context.SaveChangesAsync(cancellationToken);
        return dueQuotes.Count;
    }
}

public sealed class CotizacionExpirationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CotizacionExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expiration = scope.ServiceProvider.GetRequiredService<CotizacionExpirationService>();
                var count = await expiration.ExpireDueAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Expired {Count} quotations.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not update expired quotations; it will retry in 15 minutes.");
            }
        }
    }
}
