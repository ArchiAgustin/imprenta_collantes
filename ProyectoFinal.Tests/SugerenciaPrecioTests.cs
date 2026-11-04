using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class SugerenciaPrecioTests
{
    [Fact]
    public void MinimumPricePreservesTenPercentOnNetSale()
    {
        Assert.Equal(111.12m, SugerenciaPrecioService.CalcularMinimo(100m));
        Assert.Equal(100m, SugerenciaPrecioService.CalcularMinimo(90m));
        Assert.Null(SugerenciaPrecioService.CalcularMinimo(0m));
        Assert.Null(SugerenciaPrecioService.CalcularMinimo(null));
        Assert.True((111.12m - 100m) / 111.12m >= .10m);
        Assert.True((111.11m - 100m) / 111.11m < .10m);
    }

    [Fact]
    public async Task SuggestionsUseKnownCostsAndOnlyConfirmedHistoryOfSelectedClient()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var cliente = new Cliente { Nombre = "Cliente" };
        var otro = new Cliente { Nombre = "Otro cliente" };
        var articulo = new Producto { Nombre = "Volantes", Precio = 150m, Stock = 100, CostoUnitarioSinIgv = 100m };
        var servicioSinCosto = new Producto { Nombre = "Diseño", Precio = 50m, EsServicio = true };
        var articuloBajoPiso = new Producto { Nombre = "Cuadernos", Precio = 100m, CostoUnitarioSinIgv = 120m };
        db.AddRange(cliente, otro, articulo, servicioSinCosto, articuloBajoPiso);
        await db.SaveChangesAsync();

        db.Venta.AddRange(
            VentaAnterior(cliente.IdCliente, articulo.IdProducto, EstadoVenta.Confirmada, 125m, 20, 1),
            VentaAnterior(cliente.IdCliente, articulo.IdProducto, EstadoVenta.Cancelada, 115m, 5, 2),
            VentaAnterior(otro.IdCliente, articulo.IdProducto, EstadoVenta.Confirmada, 112m, 5, 3));
        await db.SaveChangesAsync();

        var servicio = new SugerenciaPrecioService(db);
        var propuestas = await servicio.ObtenerAsync(cliente.IdCliente, new[]
        {
            new CotizacionLineaInputViewModel { ProductoId = articulo.IdProducto, Cantidad = 40 },
            new CotizacionLineaInputViewModel { ProductoId = servicioSinCosto.IdProducto, Cantidad = 1 },
            new CotizacionLineaInputViewModel { ProductoId = articuloBajoPiso.IdProducto, Cantidad = 1 }
        });
        var item = propuestas[0];
        Assert.Equal(111.12m, item.PrecioMinimoSinIgv);
        Assert.Equal(25.92m, item.DescuentoMaximoPorcentaje);
        Assert.Equal(125m, item.UltimoPrecioClienteSinIgv);
        Assert.Equal(20, item.UltimaCantidadCliente);
        Assert.Equal(125m, item.PrecioHistoricoAplicableSinIgv);
        Assert.Null(propuestas[1].PrecioMinimoSinIgv);
        Assert.Null(propuestas[1].DescuentoMaximoPorcentaje);
        Assert.True(propuestas[1].CostoEstimado);
        Assert.Equal(133.34m, propuestas[2].PrecioMinimoSinIgv);
        Assert.Null(propuestas[2].DescuentoMaximoPorcentaje);

        var menorVolumen = await servicio.ObtenerAsync(cliente.IdCliente, new[]
        {
            new CotizacionLineaInputViewModel { ProductoId = articulo.IdProducto, Cantidad = 10 }
        });
        Assert.Null(menorVolumen[0].PrecioHistoricoAplicableSinIgv);
    }

    [Fact]
    public async Task SaleRefusesQuoteBelowCurrentMarginWithoutChangingStockOrState()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "seller-margin" };
        var client = new Cliente { Nombre = "Cliente" };
        var product = new Producto { Nombre = "Material", Precio = 150m, Stock = 1, CostoUnitarioSinIgv = 100m };
        db.AddRange(user, client, product);
        await db.SaveChangesAsync();
        var quote = new Cotizacion
        {
            IdUsuario = user.Id,
            IdCliente = client.IdCliente,
            Estado = "Emitida",
            Total = 131.11m
        };
        quote.DetalleCotizacions.Add(new DetalleCotizacion
        {
            IdProducto = product.IdProducto, Cantidad = 1, PrecioUnitario = 111.11m
        });
        db.Cotizacion.Add(quote);
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "Test"));

        var rejected = await new VentaService(db).ConfirmarCotizacionAsync(quote.IdCotizacion, user.Id, actor);
        Assert.False(rejected.Exito);
        Assert.Contains("margen mínimo del 10 %", rejected.Mensaje);
        Assert.Equal("Emitida", quote.Estado);
        Assert.Equal(1, product.Stock);
        Assert.Empty(await db.Venta.ToListAsync());

        quote.DetalleCotizacions.Single().PrecioUnitario = 111.12m;
        quote.Total = 131.12m;
        await db.SaveChangesAsync();
        var confirmed = await new VentaService(db).ConfirmarCotizacionAsync(quote.IdCotizacion, user.Id, actor);
        Assert.True(confirmed.Exito, confirmed.Mensaje);
        Assert.Equal(0, product.Stock);
    }

    private static Venta VentaAnterior(int clienteId, int productoId, string estado, decimal precio, int cantidad, int cotizacionId)
    {
        var venta = new Venta
        {
            IdCliente = clienteId,
            IdCotizacion = cotizacionId,
            FechaVentaUtc = new DateTimeOffset(2026, 10, cotizacionId, 12, 0, 0, TimeSpan.Zero),
            Estado = estado,
            Total = precio * cantidad * 1.18m
        };
        venta.Detalles.Add(new DetalleVenta
        {
            IdProducto = productoId,
            ProductoNombre = "Volantes",
            Cantidad = cantidad,
            PrecioUnitario = precio
        });
        return venta;
    }
}
