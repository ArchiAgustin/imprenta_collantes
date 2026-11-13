using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class InventarioSqlTests
{
    [Fact]
    public async Task PhysicalOpeningSalesReturnsAndCutsAreAtomicOnSqlServer()
    {
        var masterConnection = Environment.GetEnvironmentVariable("PROYECTOFINAL_SQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(masterConnection)) return;
        var name = "ProyectoFinal_Test_" + Guid.NewGuid().ToString("N");
        var masterString = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = "master" }.ConnectionString;
        var testString = new SqlConnectionStringBuilder(masterString) { InitialCatalog = name }.ConnectionString;
        await using var master = new SqlConnection(masterString);
        await master.OpenAsync();
        await using (var create = new SqlCommand($"CREATE DATABASE [{name}]", master))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(testString).Options;
            int productId, zeroProductId, quoteId, clientId, warehouseId;
            string userId;
            await using (var seed = new ApplicationDbContext(options))
            {
                await seed.Database.MigrateAsync();
                warehouseId = (await seed.Almacenes.SingleAsync()).IdAlmacen;
                Assert.Empty(await seed.SaldosAlmacen.ToListAsync());
                var unit = await seed.UnidadesMedida.SingleAsync();
                var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "inventario-" + Guid.NewGuid().ToString("N"), SecurityStamp = Guid.NewGuid().ToString() };
                var client = new Cliente { Nombre = "Cliente inventario" };
                seed.AddRange(user, client);
                await seed.SaveChangesAsync();
                userId = user.Id;
                clientId = client.IdCliente;
                var product = new Producto { Nombre = "Artículo físico", Stock = 99, Precio = 10, IdUnidadMedidaBase = unit.IdUnidadMedida };
                var zeroProduct = new Producto { Nombre = "Sin unidades físicas", Stock = 30, Precio = 10, IdUnidadMedidaBase = unit.IdUnidadMedida };
                seed.AddRange(product, zeroProduct);
                await seed.SaveChangesAsync();
                productId = product.IdProducto;
                zeroProductId = zeroProduct.IdProducto;
                seed.PresentacionesProducto.Add(new PresentacionProducto { IdProducto = productId, IdUnidadMedida = unit.IdUnidadMedida, Descripcion = "Unidad", Factor = 1 });
                seed.PresentacionesProducto.Add(new PresentacionProducto { IdProducto = zeroProductId, IdUnidadMedida = unit.IdUnidadMedida, Descripcion = "Unidad", Factor = 1 });
                var quote = new Cotizacion { IdCliente = client.IdCliente, IdUsuario = userId, FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow), Total = 23.60m };
                quote.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = productId, Cantidad = 2, PrecioUnitario = 10 });
                seed.Cotizacion.Add(quote);
                await seed.SaveChangesAsync();
                quoteId = quote.IdCotizacion;
            }

            var actor = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test"));
            int inventoryId;
            await using (var db = new ApplicationDbContext(options))
            {
                var inventory = new InventarioService(db);
                var applied = await inventory.CrearInventarioAsync(warehouseId, DateOnly.FromDateTime(DateTime.UtcNow),
                    new[] { new InventarioLineaInput(productId, 3), new InventarioLineaInput(zeroProductId, 0) }, userId, actor);
                Assert.True(applied.Exito, applied.Mensaje);
                inventoryId = applied.IdInventario!.Value;
                var header = await db.Inventarios.Include(item => item.Almacen).Include(item => item.Detalles)
                    .ThenInclude(item => item.Producto).SingleAsync(item => item.IdInventario == inventoryId);
                Assert.Equal("INV1-00000001", header.Numero);
                Assert.Equal(2, header.Detalles.Count);
                Assert.Equal("Almacén principal", header.Almacen.Nombre);
                Assert.Contains(header.Detalles, detail => detail.Producto.IdProducto == productId && detail.CantidadContadaBase == 3m);
                Assert.False(await db.Almacenes.Where(item => item.IdAlmacen == warehouseId).Select(item => item.ConteoEnCurso).SingleAsync());
            }
            await using (var db = new ApplicationDbContext(options))
            {
                Assert.Equal(99, await db.Producto.Where(p => p.IdProducto == productId).Select(p => p.Stock).SingleAsync());
                Assert.Equal(3m, await db.SaldosAlmacen.Where(s => s.IdProducto == productId).Select(s => s.CantidadBase).SingleAsync());
                Assert.Equal(0m, await db.SaldosAlmacen.Where(s => s.IdProducto == zeroProductId).Select(s => s.CantidadBase).SingleAsync());
                var sale = await new VentaService(db).ConfirmarCotizacionAsync(quoteId, userId, actor);
                Assert.True(sale.Exito, sale.Mensaje);
                Assert.Equal("V001-004532", (await db.Venta.AsNoTracking().SingleAsync(v => v.IdVenta == sale.IdVenta)).NumeroInterno);
                Assert.Equal(1m, await db.SaldosAlmacen.Where(s => s.IdProducto == productId).Select(s => s.CantidadBase).SingleAsync());
                Assert.Equal(99, await db.Producto.Where(p => p.IdProducto == productId).Select(p => p.Stock).SingleAsync());
                Assert.Equal(2, await db.Kardex.CountAsync(k => k.IdProducto == productId));
                var cancelled = await new VentaService(db).CancelarAsync(sale.IdVenta!.Value, actor);
                Assert.True(cancelled.Exito, cancelled.Mensaje);
                Assert.Equal(3m, await db.SaldosAlmacen.Where(s => s.IdProducto == productId).Select(s => s.CantidadBase).SingleAsync());
                Assert.Equal(3, await db.Kardex.CountAsync(k => k.IdProducto == productId));
            }
            int firstQuoteId, secondQuoteId;
            await using (var db = new ApplicationDbContext(options))
            {
                var missing = new Cotizacion { IdCliente = clientId, IdUsuario = userId, FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow), Total = 47.20m };
                missing.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = productId, Cantidad = 4, PrecioUnitario = 10 });
                var first = new Cotizacion { IdCliente = clientId, IdUsuario = userId, FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow), Total = 23.60m };
                first.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = productId, Cantidad = 2, PrecioUnitario = 10 });
                var second = new Cotizacion { IdCliente = clientId, IdUsuario = userId, FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow), Total = 23.60m };
                second.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = productId, Cantidad = 2, PrecioUnitario = 10 });
                db.Cotizacion.AddRange(missing, first, second);
                await db.SaveChangesAsync();
                firstQuoteId = first.IdCotizacion;
                secondQuoteId = second.IdCotizacion;
                Assert.False((await new VentaService(db).ConfirmarCotizacionAsync(missing.IdCotizacion, userId, actor)).Exito);
                Assert.Equal("Emitida", (await db.Cotizacion.AsNoTracking().SingleAsync(q => q.IdCotizacion == missing.IdCotizacion)).Estado);
                Assert.Equal(3m, await db.SaldosAlmacen.Where(s => s.IdProducto == productId).Select(s => s.CantidadBase).SingleAsync());
            }
            await using (var firstDb = new ApplicationDbContext(options))
            await using (var secondDb = new ApplicationDbContext(options))
            {
                var outcomes = await Task.WhenAll(
                    new VentaService(firstDb).ConfirmarCotizacionAsync(firstQuoteId, userId, actor),
                    new VentaService(secondDb).ConfirmarCotizacionAsync(secondQuoteId, userId, actor));
                Assert.Single(outcomes, result => result.Exito);
                await using var check = new ApplicationDbContext(options);
                var winningId = outcomes.Single(result => result.Exito).IdVenta;
                Assert.Equal("V001-004533", (await check.Venta.AsNoTracking().SingleAsync(v => v.IdVenta == winningId)).NumeroInterno);
                Assert.Equal(1m, await check.SaldosAlmacen.Where(s => s.IdProducto == productId).Select(s => s.CantidadBase).SingleAsync());
                Assert.True((await new VentaService(check).CancelarAsync(outcomes.Single(result => result.Exito).IdVenta!.Value, actor)).Exito);
                Assert.Equal(3m, await check.SaldosAlmacen.Where(s => s.IdProducto == productId).Select(s => s.CantidadBase).SingleAsync());
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var inventory = new InventarioService(db);
                var cut = await inventory.CrearInventarioAsync(warehouseId, DateOnly.FromDateTime(DateTime.UtcNow),
                    new[] { new InventarioLineaInput(productId, 2), new InventarioLineaInput(zeroProductId, 0) }, userId, actor);
                Assert.True(cut.Exito, cut.Mensaje);
                Assert.Equal(2m, await db.SaldosAlmacen.Where(s => s.IdProducto == productId).Select(s => s.CantidadBase).SingleAsync());
                Assert.Equal("Ajuste salida", (await db.Kardex.OrderByDescending(k => k.IdKardex).FirstAsync()).TipoMovimiento);
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var drop = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", master);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
