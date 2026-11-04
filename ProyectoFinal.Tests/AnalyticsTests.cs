using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class AnalyticsTests
{
    [Fact]
    public async Task ConfirmedSalesUsePeruConfirmationDateIgvAndHistoricalCosts()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "analytics-test" };
        var customer = new Cliente { Nombre = "Cliente de prueba" };
        var article = new Producto { Nombre = "Artículo", Precio = 10, Stock = 10, CostoUnitarioSinIgv = 4m };
        var service = new Producto { Nombre = "Servicio", Precio = 20, Stock = 0, EsServicio = true };
        db.AddRange(user, customer, article, service);
        await db.SaveChangesAsync();

        var quote = new Cotizacion
        {
            IdCliente = customer.IdCliente,
            IdUsuario = user.Id,
            FechaCreacion = new DateOnly(2026, 10, 23),
            Estado = "Emitida",
            Total = 47.20m
        };
        quote.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = article.IdProducto, Cantidad = 2, PrecioUnitario = 10m });
        quote.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = service.IdProducto, Cantidad = 1, PrecioUnitario = 20m });
        db.Cotizacion.Add(quote);
        await db.SaveChangesAsync();

        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "Test"));
        var result = await new VentaService(db).ConfirmarCotizacionAsync(quote.IdCotizacion, user.Id, actor);
        Assert.True(result.Exito, result.Mensaje);
        var sale = await db.Venta.Include(item => item.Detalles).SingleAsync();
        Assert.Equal(4m, sale.Detalles.Single(line => !line.EsServicio).CostoUnitarioSinIgv);
        Assert.Null(sale.Detalles.Single(line => line.EsServicio).CostoUnitarioSinIgv);

        article.CostoUnitarioSinIgv = 9m;
        sale.FechaVentaUtc = new DateTimeOffset(2026, 10, 24, 3, 30, 0, TimeSpan.Zero); // 23 Oct 22:30 Peru
        db.Venta.Add(new Venta
        {
            IdCotizacion = 9001,
            IdCliente = customer.IdCliente,
            FechaVentaUtc = new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.Zero),
            Total = 100m,
            Estado = EstadoVenta.Cancelada
        });
        await db.SaveChangesAsync();

        var analytics = new AnalyticsService(db);
        var report = await analytics.ObtenerAsync(new DateOnly(2026, 10, 23), new DateOnly(2026, 10, 23), 5);
        Assert.Equal(1, report.VentasConfirmadas);
        Assert.Equal(1, report.VentasCanceladas);
        Assert.Equal(47.20m, report.VentasConIgv);
        Assert.Equal(40m, report.VentasSinIgv);
        Assert.Equal(7.20m, report.Igv);
        Assert.Equal(8m, report.CostoConocidoSinIgv);
        Assert.Equal(12m, report.UtilidadEstimada);
        Assert.Equal(1, report.LineasSinCosto);
        Assert.Equal(2, report.TotalLineas);
        Assert.Equal(1, report.CotizacionesConvertidas);
        Assert.Equal(1, report.CotizacionesEmitidas);
        Assert.Equal(1, report.EnCola);
        Assert.Single(report.Clientes);
        Assert.Equal(47.20m, report.Productos.Sum(item => item.TotalConIgv));
        Assert.Empty(report.AlertasStock); // article has 8 units; service is excluded.

        var nextDay = await analytics.ObtenerAsync(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 24), 10);
        Assert.Equal(0, nextDay.VentasConfirmadas);
        Assert.Null(nextDay.UtilidadEstimada);
        Assert.Single(nextDay.AlertasStock);
        Assert.Equal(article.IdProducto, nextDay.AlertasStock[0].IdProducto);
    }

    [Fact]
    public async Task SaleWithoutKnownCostsDoesNotInventProfit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var customer = new Cliente { Nombre = "Cliente" };
        db.Add(customer);
        await db.SaveChangesAsync();
        var sale = new Venta
        {
            IdCotizacion = 10001,
            IdCliente = customer.IdCliente,
            FechaVentaUtc = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero),
            Total = 11.80m,
            Estado = EstadoVenta.Confirmada
        };
        sale.Detalles.Add(new DetalleVenta { IdProducto = 1, Cantidad = 1, PrecioUnitario = 10m, CostoUnitarioSinIgv = null });
        db.Add(sale);
        await db.SaveChangesAsync();

        var report = await new AnalyticsService(db).ObtenerAsync(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 24), 5);
        Assert.Equal(11.80m, report.VentasConIgv);
        Assert.Null(report.UtilidadEstimada);
        Assert.Null(report.MargenEstimadoPorcentaje);
    }
}
