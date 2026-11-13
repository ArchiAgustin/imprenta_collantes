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

    [Fact]
    public async Task DashboardMetricsCoverFunnelParetoAbcTicketOrdersStockAndPresentationFactors()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);

        var customers = new[]
        {
            new Cliente { Nombre = "Cliente A" },
            new Cliente { Nombre = "Cliente B" },
            new Cliente { Nombre = "Cliente C" }
        };
        var products = new[]
        {
            new Producto { Nombre = "Producto A", EsServicio = false, CostoUnitarioSinIgv = 2m },
            new Producto { Nombre = "Producto B", EsServicio = false },
            new Producto { Nombre = "Producto C", EsServicio = false }
        };
        db.AddRange(customers);
        db.AddRange(products);
        var warehouse = new Almacen { Nombre = "Analítica", Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow };
        db.Almacenes.Add(warehouse);
        await db.SaveChangesAsync();

        var quoteA = new Cotizacion { IdCliente = customers[0].IdCliente, Estado = "Convertida", FechaCreacion = new DateOnly(2026, 10, 5), Total = 70m };
        var quoteB = new Cotizacion { IdCliente = customers[1].IdCliente, Estado = "Convertida", FechaCreacion = new DateOnly(2026, 10, 10), Total = 20m };
        var quoteC = new Cotizacion { IdCliente = customers[2].IdCliente, Estado = "Convertida", FechaCreacion = new DateOnly(2026, 11, 2), Total = 10m };
        var quoteOpen = new Cotizacion { IdCliente = customers[0].IdCliente, Estado = "Emitida", FechaCreacion = new DateOnly(2026, 10, 20), FechaVencimiento = FechaComercial.HoyPeru().AddDays(1), Total = 5m };
        var quoteDue = new Cotizacion { IdCliente = customers[1].IdCliente, Estado = "Emitida", FechaCreacion = new DateOnly(2026, 10, 21), FechaVencimiento = FechaComercial.HoyPeru().AddDays(-1), Total = 5m };
        var quoteExpired = new Cotizacion { IdCliente = customers[2].IdCliente, Estado = "Vencida", FechaCreacion = new DateOnly(2026, 10, 22), Total = 5m };
        db.Cotizacion.AddRange(quoteA, quoteB, quoteC, quoteOpen, quoteDue, quoteExpired);
        await db.SaveChangesAsync();

        var octoberA = new Venta
        {
            IdCotizacion = quoteA.IdCotizacion, IdCliente = customers[0].IdCliente,
            FechaVentaUtc = new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero),
            Total = 70m, Estado = EstadoVenta.Confirmada
        };
        octoberA.Detalles.Add(new DetalleVenta
        {
            IdProducto = products[0].IdProducto, ProductoNombre = "Producto A", Cantidad = 1,
            PrecioUnitario = 59.32m, CostoUnitarioSinIgv = 2m, FactorPresentacion = 10m,
            PresentacionDescripcion = "Paquete de 10", UnidadBaseDescripcion = "Unidad"
        });
        var octoberB = new Venta
        {
            IdCotizacion = quoteB.IdCotizacion, IdCliente = customers[1].IdCliente,
            FechaVentaUtc = new DateTimeOffset(2026, 10, 22, 18, 0, 0, TimeSpan.Zero),
            Total = 20m, Estado = EstadoVenta.Confirmada
        };
        octoberB.Detalles.Add(new DetalleVenta
        {
            IdProducto = products[1].IdProducto, ProductoNombre = "Producto B", Cantidad = 1,
            PrecioUnitario = 16.95m, FactorPresentacion = 1m
        });
        var novemberC = new Venta
        {
            IdCotizacion = quoteC.IdCotizacion, IdCliente = customers[2].IdCliente,
            FechaVentaUtc = new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero),
            Total = 10m, Estado = EstadoVenta.Confirmada
        };
        novemberC.Detalles.Add(new DetalleVenta
        {
            IdProducto = products[2].IdProducto, ProductoNombre = "Producto C", Cantidad = 1,
            PrecioUnitario = 8.47m, FactorPresentacion = 1m
        });
        var cancelled = new Venta
        {
            IdCotizacion = 800, IdCliente = customers[0].IdCliente,
            FechaVentaUtc = new DateTimeOffset(2026, 11, 3, 18, 0, 0, TimeSpan.Zero),
            Total = 1000m, Estado = EstadoVenta.Cancelada
        };
        cancelled.Detalles.Add(new DetalleVenta
        {
            IdProducto = products[0].IdProducto, ProductoNombre = "Producto A", Cantidad = 100,
            PrecioUnitario = 8.47m, FactorPresentacion = 10m, CostoUnitarioSinIgv = 2m
        });
        db.Venta.AddRange(octoberA, octoberB, novemberC, cancelled);
        db.Pedido.AddRange(
            new Pedido { IdCliente = customers[0].IdCliente, EstadoPedido = EstadoPedido.EnCola },
            new Pedido { IdCliente = customers[0].IdCliente, EstadoPedido = EstadoPedido.EnProduccion },
            new Pedido { IdCliente = customers[1].IdCliente, EstadoPedido = EstadoPedido.ListoParaRecojo },
            new Pedido { IdCliente = customers[1].IdCliente, EstadoPedido = EstadoPedido.Entregado },
            new Pedido { IdCliente = customers[2].IdCliente, EstadoPedido = EstadoPedido.Cancelado });
        db.SaldosAlmacen.AddRange(
            new StockAlmacen { IdAlmacen = warehouse.IdAlmacen, IdProducto = products[0].IdProducto, CantidadBase = 1m },
            new StockAlmacen { IdAlmacen = warehouse.IdAlmacen, IdProducto = products[1].IdProducto, CantidadBase = 2m },
            new StockAlmacen { IdAlmacen = warehouse.IdAlmacen, IdProducto = products[2].IdProducto, CantidadBase = 0m });
        await db.SaveChangesAsync();

        var report = await new AnalyticsService(db).ObtenerAsync(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), 1);

        Assert.Equal(3, report.VentasConfirmadas);
        Assert.Equal(1, report.VentasCanceladas);
        Assert.Equal(100m, report.VentasConIgv);
        Assert.Equal(84.74m, report.VentasSinIgv);
        Assert.Equal(15.26m, report.Igv);
        Assert.Equal(6, report.CotizacionesEmitidas);
        Assert.Equal(3, report.CotizacionesConvertidas);
        Assert.Equal(2, report.CotizacionesVencidas);
        Assert.Equal(1, report.CotizacionesVigentes);
        Assert.Equal(50m, report.TasaConversionPorcentaje);
        Assert.Equal(1, report.EnCola);
        Assert.Equal(1, report.EnProduccion);
        Assert.Equal(1, report.ListosParaRecojo);
        Assert.Equal(1, report.Entregados);
        Assert.Equal(1, report.PedidosCancelados);
        Assert.Equal(3, report.Tendencia.Count);
        Assert.Equal(70m, report.Productos[0].PorcentajeAcumulado);
        Assert.Equal(90m, report.Productos[1].PorcentajeAcumulado);
        Assert.Equal(100m, report.Productos[2].PorcentajeAcumulado);
        Assert.Equal(10m, report.Productos[0].Unidades);
        Assert.Equal(39.32m, report.UtilidadEstimada);
        Assert.Equal(2, report.LineasSinCosto);
        Assert.Equal("A", report.Clientes.Single(item => item.IdCliente == customers[0].IdCliente).Segmento);
        Assert.Equal("B", report.Clientes.Single(item => item.IdCliente == customers[1].IdCliente).Segmento);
        Assert.Equal("C", report.Clientes.Single(item => item.IdCliente == customers[2].IdCliente).Segmento);
        Assert.Equal(70m, report.Clientes.Single(item => item.IdCliente == customers[0].IdCliente).TicketPromedio);
        Assert.Equal(new[] { products[2].IdProducto, products[0].IdProducto }, report.AlertasStock.Select(item => item.IdProducto).ToArray());
    }

    [Fact]
    public async Task ClientRecurrenceUsesPeriodPurchasesAndFullHistory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);

        var recurrente = new Cliente { Nombre = "Cliente recurrente" };
        var nuevo = new Cliente { Nombre = "Cliente nuevo" };
        db.AddRange(recurrente, nuevo);
        await db.SaveChangesAsync();

        var hoy = FechaComercial.HoyPeru();
        var primera = hoy.AddDays(-40);
        var segunda = hoy.AddDays(-10);
        var ultima = hoy.AddDays(-4);

        static DateTimeOffset Momento(DateOnly fecha) => new(fecha.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero);

        db.Venta.AddRange(
            new Venta { IdCotizacion = 1, IdCliente = recurrente.IdCliente, FechaVentaUtc = Momento(primera), Total = 100m, Estado = EstadoVenta.Confirmada },
            new Venta { IdCotizacion = 2, IdCliente = recurrente.IdCliente, FechaVentaUtc = Momento(segunda), Total = 60m, Estado = EstadoVenta.Confirmada },
            new Venta { IdCotizacion = 3, IdCliente = recurrente.IdCliente, FechaVentaUtc = Momento(ultima), Total = 40m, Estado = EstadoVenta.Confirmada },
            new Venta { IdCotizacion = 4, IdCliente = nuevo.IdCliente, FechaVentaUtc = Momento(segunda), Total = 30m, Estado = EstadoVenta.Confirmada },
            new Venta { IdCotizacion = 5, IdCliente = nuevo.IdCliente, FechaVentaUtc = Momento(hoy.AddDays(-20)), Total = 999m, Estado = EstadoVenta.Cancelada });
        await db.SaveChangesAsync();

        var report = await new AnalyticsService(db).ObtenerAsync(hoy.AddDays(-30), hoy, 0);

        Assert.Equal(2, report.ClientesConVentas);
        Assert.Equal(1, report.ClientesRecurrentes);
        Assert.Equal(50m, report.PorcentajeClientesRecurrentes);
        Assert.Equal(50m, report.TicketMedioRecurrente); // (60 + 40) / 2 en el periodo
        Assert.Equal(30m, report.TicketMedioNuevo);

        var filaRecurrente = report.Clientes.Single(item => item.IdCliente == recurrente.IdCliente);
        Assert.True(filaRecurrente.Recurrente);
        Assert.Equal(2, filaRecurrente.Ventas);
        Assert.Equal(ultima, filaRecurrente.UltimaCompra);
        Assert.Equal(4, filaRecurrente.DiasDesdeUltimaCompra);
        Assert.Equal(18m, filaRecurrente.FrecuenciaMediaDias); // (última - primera) / (3 - 1)

        var filaNueva = report.Clientes.Single(item => item.IdCliente == nuevo.IdCliente);
        Assert.False(filaNueva.Recurrente);
        Assert.Equal(segunda, filaNueva.UltimaCompra);
        Assert.Equal(10, filaNueva.DiasDesdeUltimaCompra);
        Assert.Null(filaNueva.FrecuenciaMediaDias);
    }
}
