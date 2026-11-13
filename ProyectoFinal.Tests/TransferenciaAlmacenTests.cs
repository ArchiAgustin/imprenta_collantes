using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public sealed class TransferenciaAlmacenTests
{
    [Fact]
    public async Task SuccessfulTransferMovesStockAndWritesPairedKardexEntries()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var source = new Almacen { Nombre = "Almacén principal", EsPredeterminado = true, Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow };
        var destination = new Almacen { Nombre = "Producción", Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow };
        var product = new Producto { Nombre = "Papel", Stock = 0, Precio = 1 };
        db.AddRange(source, destination, product);
        await db.SaveChangesAsync();
        db.SaldosAlmacen.AddRange(
            new StockAlmacen { IdAlmacen = source.IdAlmacen, IdProducto = product.IdProducto, CantidadBase = 8m },
            new StockAlmacen { IdAlmacen = destination.IdAlmacen, IdProducto = product.IdProducto, CantidadBase = 2m });
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "operator") }, "Test"));

        var result = await new TransferenciaAlmacenService(db, new InventarioService(db))
            .TransferirAsync(source.IdAlmacen, destination.IdAlmacen, product.IdProducto, 3m, "Reposición", "operator", actor);

        Assert.True(result.Exito, result.Mensaje);
        Assert.Equal(5m, (await db.SaldosAlmacen.FindAsync(source.IdAlmacen, product.IdProducto))!.CantidadBase);
        Assert.Equal(5m, (await db.SaldosAlmacen.FindAsync(destination.IdAlmacen, product.IdProducto))!.CantidadBase);
        var entries = await db.Kardex.OrderBy(item => item.IdAlmacen).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, item => item.IdAlmacen == source.IdAlmacen && item.TipoMovimiento == "Transferencia salida"
            && item.SaldoAnteriorBase == 8m && item.SaldoPosteriorBase == 5m);
        Assert.Contains(entries, item => item.IdAlmacen == destination.IdAlmacen && item.TipoMovimiento == "Transferencia entrada"
            && item.SaldoAnteriorBase == 2m && item.SaldoPosteriorBase == 5m);
        Assert.Single(await db.TransferenciasAlmacen.ToListAsync());
    }

    [Fact]
    public async Task InsufficientSourceBalanceDoesNotChangeEitherWarehouse()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var source = new Almacen { Nombre = "Origen", Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow };
        var destination = new Almacen { Nombre = "Destino", Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow };
        var product = new Producto { Nombre = "Papel", Stock = 0, Precio = 1 };
        db.AddRange(source, destination, product);
        await db.SaveChangesAsync();
        db.SaldosAlmacen.Add(new StockAlmacen { IdAlmacen = source.IdAlmacen, IdProducto = product.IdProducto, CantidadBase = 1m });
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await new TransferenciaAlmacenService(db, new InventarioService(db))
            .TransferirAsync(source.IdAlmacen, destination.IdAlmacen, product.IdProducto, 2m, "Reposición", null, actor);

        Assert.False(result.Exito);
        Assert.Equal(1m, (await db.SaldosAlmacen.FindAsync(source.IdAlmacen, product.IdProducto))!.CantidadBase);
        Assert.Null(await db.SaldosAlmacen.FindAsync(destination.IdAlmacen, product.IdProducto));
        Assert.Empty(await db.Kardex.ToListAsync());
        Assert.Empty(await db.TransferenciasAlmacen.ToListAsync());
    }
}
