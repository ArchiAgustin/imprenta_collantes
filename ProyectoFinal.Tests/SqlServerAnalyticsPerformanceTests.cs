using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Services;
using Xunit;
using Xunit.Abstractions;

namespace ProyectoFinal.Tests;

public sealed class SqlServerAnalyticsPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ReadOnlyAnalyticsProjectionMeetsTheOnePointFiveSecondTargetOnConfiguredSqlServer()
    {
        var connectionString = Environment.GetEnvironmentVariable("PROYECTOFINAL_SQL_ANALYTICS_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            output.WriteLine("Medición no ejecutada: configura PROYECTOFINAL_SQL_ANALYTICS_CONNECTION con una base SQL representativa.");
            return;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new ApplicationDbContext(options);
        var today = FechaComercial.HoyPeru();
        var desde = today.AddDays(-365);
        var inicio = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-5)).ToUniversalTime();
        var fin = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-5)).ToUniversalTime();
        var saleIds = await db.Venta.AsNoTracking()
            .Where(item => item.FechaVentaUtc >= inicio && item.FechaVentaUtc < fin)
            .Select(item => item.IdVenta)
            .ToArrayAsync();
        var lineCount = saleIds.Length == 0 ? 0 : await db.DetalleVenta.AsNoTracking()
            .CountAsync(item => saleIds.Contains(item.IdVenta));
        output.WriteLine($"Volumen medido (366 días): {saleIds.Length} ventas, {lineCount} líneas. Confirma que representa la carga esperada en producción.");
        var service = new AnalyticsService(db);
        // Warm EF query compilation and the SQL connection pool, then time a normal dashboard request.
        await service.ObtenerAsync(desde, today, 5);
        var stopwatch = Stopwatch.StartNew();
        var report = await service.ObtenerAsync(desde, today, 5);
        stopwatch.Stop();

        Assert.NotNull(report);
        output.WriteLine($"Tiempo de consulta tras calentamiento: {stopwatch.Elapsed.TotalMilliseconds:0.0} ms.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(1500),
            $"The 366-day analytics projection took {stopwatch.Elapsed.TotalMilliseconds:0} ms, exceeding the 1.5 second target.");
    }
}
