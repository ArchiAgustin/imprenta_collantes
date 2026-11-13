using System.Data;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class CotizacionExpirationService(ApplicationDbContext context)
{
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var today = FechaComercial.HoyPeru();
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        var dueIds = await context.Cotizacion.AsNoTracking()
            .Where(quote => quote.Estado == "Emitida"
                && quote.FechaVencimiento != null
                && quote.FechaVencimiento < today)
            .Select(quote => quote.IdCotizacion)
            .ToListAsync(cancellationToken);
        if (dueIds.Count == 0)
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var expired = 0;
        foreach (var id in dueIds)
        {
            if (transaction is not null)
            {
                // Multiple app instances/workers may scan the same expired quote.
                // Only the process that atomically changes Emitida -> Vencida records it.
                var changed = await context.Cotizacion
                    .Where(quote => quote.IdCotizacion == id && quote.Estado == "Emitida"
                        && quote.FechaVencimiento != null && quote.FechaVencimiento < today)
                    .ExecuteUpdateAsync(update => update.SetProperty(quote => quote.Estado, "Vencida"), cancellationToken);
                if (changed != 1) continue;
            }
            else
            {
                var quote = await context.Cotizacion.FirstOrDefaultAsync(item => item.IdCotizacion == id
                    && item.Estado == "Emitida" && item.FechaVencimiento != null && item.FechaVencimiento < today,
                    cancellationToken);
                if (quote is null) continue;
                quote.Estado = "Vencida";
            }

            context.AuditoriaOperacion.Add(AuditoriaFactory.CrearSistema(
                "Cotizacion", id, "Vencimiento automático", "Emitida", "Vencida"));
            expired++;
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return expired;
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
