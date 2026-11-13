using System.Security.Claims;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinal.Controllers;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class SqlServerWorkflowTests
{
    // Opt-in: run with PROYECTOFINAL_SQL_TEST_CONNECTION pointing at master on a local SQL Server.
    // This test owns and removes only its uniquely named, temporary database.
    [Fact]
    public async Task CancelAndStartCannotBothSucceedOnSqlServer()
    {
        var masterConnection = Environment.GetEnvironmentVariable("PROYECTOFINAL_SQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(masterConnection)) return;

        var databaseName = "ProyectoFinal_Test_" + Guid.NewGuid().ToString("N");
        var masterBuilder = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = "master" };
        var databaseBuilder = new SqlConnectionStringBuilder(masterBuilder.ConnectionString) { InitialCatalog = databaseName };
        var connectionString = databaseBuilder.ConnectionString;

        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync();
        await using (var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", master))
            await create.ExecuteNonQueryAsync();

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.MigrateAsync();
                // The warehouse/unit exist, but balances remain empty before a count.
                Assert.Single(await setup.UnidadesMedida.ToListAsync());
                Assert.Empty(await setup.PresentacionesProducto.ToListAsync());
                Assert.Empty(await setup.SaldosAlmacen.ToListAsync());
                Assert.Empty(await setup.Kardex.ToListAsync());
            }

            // Test both deterministic orders and repeat the race on independent sales.
            foreach (var order in new[] { "cancel-first", "start-first" }
                .Concat(Enumerable.Repeat("simultaneous", 12)))
            {
                int saleId;
                int workId;
                int productId;
                ClaimsPrincipal actor;
                await using (var seed = new ApplicationDbContext(options))
                {
                    var user = new IdentityUser
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserName = "test-" + Guid.NewGuid().ToString("N"),
                        SecurityStamp = Guid.NewGuid().ToString()
                    };
                    var client = new Cliente { Nombre = "Cliente prueba SQL" };
                    var baseUnitId = await seed.UnidadesMedida.AsNoTracking().Where(item => item.Activo)
                        .Select(item => item.IdUnidadMedida).FirstAsync();
                    var product = new Producto
                    {
                        Nombre = "Material de prueba", Precio = 10m, Stock = 1,
                        CostoUnitarioSinIgv = 4m, IdUnidadMedidaBase = baseUnitId
                    };
                    seed.AddRange(user, client, product);
                    await seed.SaveChangesAsync();

                    var quote = new Cotizacion
                    {
                        IdCliente = client.IdCliente,
                        IdUsuario = user.Id,
                        FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow),
                        FechaVencimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                        Total = 11.80m
                    };
                    quote.DetalleCotizacions.Add(new DetalleCotizacion
                    {
                        IdProducto = product.IdProducto,
                        Cantidad = 1,
                        PrecioUnitario = 10m
                    });
                    seed.Cotizacion.Add(quote);
                    await seed.SaveChangesAsync();

                    actor = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "Test"));
                    var confirmed = await new VentaService(seed).ConfirmarCotizacionAsync(quote.IdCotizacion, user.Id, actor);
                    Assert.True(confirmed.Exito, confirmed.Mensaje);
                    saleId = confirmed.IdVenta!.Value;
                    Assert.Equal(4m, await seed.DetalleVenta.Where(line => line.IdVenta == saleId)
                        .Select(line => line.CostoUnitarioSinIgv).SingleAsync());
                    workId = await seed.Pedido.Where(item => item.IdVenta == saleId)
                        .Select(item => item.IdPedido).SingleAsync();
                    productId = product.IdProducto;
                    Assert.Equal(0, await seed.Producto.Where(item => item.IdProducto == productId)
                        .Select(item => item.Stock).SingleAsync());
                    Assert.Empty(await seed.SaldosAlmacen.ToListAsync());
                    Assert.Empty(await seed.Kardex.ToListAsync());
                }

                await using var cancelContext = new ApplicationDbContext(options);
                await using var startContext = new ApplicationDbContext(options);
                var cancellation = new VentaService(cancelContext);
                var production = new VentaService(startContext);
                ResultadoVenta cancelResult;
                ResultadoVenta startResult;

                if (order == "cancel-first")
                {
                    cancelResult = await cancellation.CancelarAsync(saleId, actor);
                    startResult = await production.AvanzarPedidoAsync(workId, EstadoPedido.EnProduccion, actor);
                }
                else if (order == "start-first")
                {
                    startResult = await production.AvanzarPedidoAsync(workId, EstadoPedido.EnProduccion, actor);
                    cancelResult = await cancellation.CancelarAsync(saleId, actor);
                }
                else
                {
                    var cancelTask = cancellation.CancelarAsync(saleId, actor);
                    var startTask = production.AvanzarPedidoAsync(workId, EstadoPedido.EnProduccion, actor);
                    await Task.WhenAll(cancelTask, startTask);
                    cancelResult = cancelTask.Result;
                    startResult = startTask.Result;
                }

                Assert.NotEqual(cancelResult.Exito, startResult.Exito);
                await using var verify = new ApplicationDbContext(options);
                var sale = await verify.Venta.AsNoTracking().SingleAsync(item => item.IdVenta == saleId);
                var work = await verify.Pedido.AsNoTracking().SingleAsync(item => item.IdPedido == workId);
                var stock = await verify.Producto.AsNoTracking().Where(item => item.IdProducto == productId)
                    .Select(item => item.Stock).SingleAsync();

                if (cancelResult.Exito)
                {
                    Assert.Equal(EstadoVenta.Cancelada, sale.Estado);
                    Assert.Equal(EstadoPedido.Cancelado, work.EstadoPedido);
                    Assert.Equal(1, stock);
                }
                else
                {
                    Assert.Equal(EstadoVenta.Confirmada, sale.Estado);
                    Assert.Equal(EstadoPedido.EnProduccion, work.EstadoPedido);
                    Assert.Equal(0, stock);
                }
                Assert.Equal(1, await verify.AuditoriaOperacion.CountAsync(entry => entry.TipoEntidad == "Pedido"
                    && entry.IdEntidad == workId && entry.Accion == (cancelResult.Exito ? "Trabajo cancelado" : "Estado de trabajo actualizado")));
            }

            await using var firstEditor = new ApplicationDbContext(options);
            await using var secondEditor = new ApplicationDbContext(options);
            var firstVersion = await firstEditor.Producto.FirstAsync();
            var staleVersion = await secondEditor.Producto.FirstAsync();
            Assert.Equal(8, firstVersion.RowVersion.Length);
            firstVersion.Stock = (firstVersion.Stock ?? 0) + 1;
            firstVersion.CostoUnitarioSinIgv = 6m;
            await firstEditor.SaveChangesAsync();
            staleVersion.Stock = (staleVersion.Stock ?? 0) + 2;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondEditor.SaveChangesAsync());
            Assert.Equal(4m, await firstEditor.DetalleVenta.Where(line => line.IdProducto == firstVersion.IdProducto)
                .OrderBy(line => line.IdDetalleVenta).Select(line => line.CostoUnitarioSinIgv).FirstAsync());

            await using (var editContext = new ApplicationDbContext(options))
            {
                var controller = new ProductoesController(new ProductosCatalogoService(editContext, new InventarioService(editContext)));
                var staleEdit = await controller.Edit(staleVersion.IdProducto, new Producto
                {
                    IdProducto = staleVersion.IdProducto,
                    Nombre = staleVersion.Nombre,
                    Precio = staleVersion.Precio,
                    Stock = staleVersion.Stock,
                    EsServicio = staleVersion.EsServicio,
                    RowVersion = staleVersion.RowVersion
                });
                Assert.IsType<ViewResult>(staleEdit);
                Assert.False(controller.ModelState.IsValid);
            }

            int doubleQuoteId;
            int doubleProductId;
            int packageWarehouseId;
            int packagePresentationId;
            string doubleUserId;
            await using (var seed = new ApplicationDbContext(options))
            {
                var user = new IdentityUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = "concurrent-" + Guid.NewGuid().ToString("N"),
                    SecurityStamp = Guid.NewGuid().ToString()
                };
                var client = new Cliente { Nombre = "Cliente conversión simultánea" };
                var unitId = await seed.UnidadesMedida.AsNoTracking().Where(item => item.Activo)
                    .Select(item => item.IdUnidadMedida).FirstAsync();
                var warehouse = new Almacen
                {
                    Nombre = "Almacén paquetes SQL", Activo = true,
                    FechaActivacionUtc = DateTimeOffset.UtcNow
                };
                var product = new Producto
                {
                    Nombre = "Último paquete SQL", Precio = 1m, Stock = 1,
                    IdUnidadMedidaBase = unitId
                };
                seed.AddRange(user, client, product, warehouse);
                await seed.SaveChangesAsync();
                seed.PresentacionesProducto.AddRange(
                    new PresentacionProducto
                    {
                        IdProducto = product.IdProducto, IdUnidadMedida = unitId,
                        Descripcion = "Unidad base SQL", Factor = 1m
                    },
                    new PresentacionProducto
                    {
                        IdProducto = product.IdProducto, IdUnidadMedida = unitId,
                        Descripcion = "Paquete de 10 SQL", Factor = 10m
                    });
                await seed.SaveChangesAsync();
                packagePresentationId = await seed.PresentacionesProducto
                    .Where(item => item.IdProducto == product.IdProducto && item.Factor == 10m)
                    .Select(item => item.IdPresentacionProducto).SingleAsync();
                packageWarehouseId = warehouse.IdAlmacen;
                seed.SaldosAlmacen.Add(new StockAlmacen
                {
                    IdAlmacen = packageWarehouseId, IdProducto = product.IdProducto, CantidadBase = 10m
                });
                await seed.SaveChangesAsync();
                var quote = new Cotizacion
                {
                    IdCliente = client.IdCliente,
                    IdUsuario = user.Id,
                    FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow),
                    Total = 11.80m
                };
                quote.DetalleCotizacions.Add(new DetalleCotizacion
                {
                    IdProducto = product.IdProducto,
                    IdPresentacionProducto = packagePresentationId,
                    FactorPresentacion = 10m,
                    PresentacionDescripcion = "Paquete de 10 SQL",
                    UnidadBaseDescripcion = (await seed.UnidadesMedida.FindAsync(unitId))!.Descripcion,
                    Cantidad = 1,
                    PrecioUnitario = 10m
                });
                seed.Cotizacion.Add(quote);
                await seed.SaveChangesAsync();
                doubleQuoteId = quote.IdCotizacion;
                doubleProductId = product.IdProducto;
                doubleUserId = user.Id;
            }

            var doubleActor = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, doubleUserId) }, "Test"));
            await using var firstSaleContext = new ApplicationDbContext(options);
            await using var secondSaleContext = new ApplicationDbContext(options);
            var firstSaleTask = new VentaService(firstSaleContext).ConfirmarCotizacionAsync(doubleQuoteId, packageWarehouseId, doubleUserId, doubleActor);
            var secondSaleTask = new VentaService(secondSaleContext).ConfirmarCotizacionAsync(doubleQuoteId, packageWarehouseId, doubleUserId, doubleActor);
            var doubleResults = await Task.WhenAll(firstSaleTask, secondSaleTask);
            Assert.Single(doubleResults, result => result.Exito);
            await using var check = new ApplicationDbContext(options);
            Assert.Equal("Convertida", (await check.Cotizacion.SingleAsync(item => item.IdCotizacion == doubleQuoteId)).Estado);
            Assert.Single(await check.Venta.Where(item => item.IdCotizacion == doubleQuoteId).ToListAsync());
            Assert.Single(await check.Pedido.Where(item => item.IdCotizacion == doubleQuoteId).ToListAsync());
            Assert.Equal(1, (await check.Producto.SingleAsync(item => item.IdProducto == doubleProductId)).Stock);
            Assert.Equal(0m, (await check.SaldosAlmacen.FindAsync(packageWarehouseId, doubleProductId))!.CantidadBase);
            var packageSale = await check.DetalleVenta.SingleAsync(item => item.IdProducto == doubleProductId);
            Assert.Equal(10m, packageSale.FactorPresentacion);
            Assert.Equal(1, packageSale.Cantidad);
            var packageKardex = await check.Kardex.SingleAsync(item => item.IdProducto == doubleProductId);
            Assert.Equal(10m, packageKardex.CantidadBase);

            // Execute the BI projections with the SQL Server provider as well: InMemory
            // does not catch unsupported translations for dates, grouping or cost columns.
            var hoyUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            var analyticsTimer = Stopwatch.StartNew();
            var analytics = await new AnalyticsService(check).ObtenerAsync(hoyUtc.AddDays(-1), hoyUtc.AddDays(1), 5);
            analyticsTimer.Stop();
            Assert.True(analyticsTimer.Elapsed < TimeSpan.FromMilliseconds(1500),
                $"Analytics SQL projections took {analyticsTimer.Elapsed.TotalMilliseconds:0} ms, exceeding 1.5 seconds for this acceptance fixture.");
            var salesCount = await check.Venta.CountAsync(item => item.Estado == EstadoVenta.Confirmada);
            Assert.Equal(salesCount, analytics.VentasConfirmadas);
            Assert.Equal(salesCount * 11.80m, analytics.VentasConIgv);
            Assert.Equal(salesCount * 10m, analytics.VentasSinIgv);
            Assert.True(analytics.LineasSinCosto > 0);
            Assert.True(analytics.CostoConocidoSinIgv > 0);

            // Render the admin dashboard against the same migrated SQL database,
            // including Identity sign-in and authorization at the HTTP boundary.
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["BootstrapAdmin:Email"] = "",
                    ["BootstrapAdmin:Password"] = ""
                })));
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var anonymous = await httpClient.GetAsync("/Analytics");
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var admin = new IdentityUser { UserName = "report-admin@example.test", Email = "report-admin@example.test" };
                Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
                Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
            }
            var login = await httpClient.GetStringAsync("/Identity/Account/Login");
            var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success);
            var signedIn = await httpClient.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = "report-admin@example.test",
                ["Input.Password"] = "TestPassword123!",
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
            }));
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            Assert.Contains("C001-004532", await httpClient.GetStringAsync("/SeriesInternas"));
            var dashboard = await httpClient.GetAsync("/Analytics");
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
            Assert.Contains("Ventas confirmadas con IGV", dashboardHtml);
            Assert.Contains("Utilidad estimada parcial", dashboardHtml);
            var inventoryPage = await httpClient.GetAsync("/Inventario");
            Assert.Equal(HttpStatusCode.OK, inventoryPage.StatusCode);
            Assert.Contains("Apertura inicial", await inventoryPage.Content.ReadAsStringAsync());

            var firstClientId = await check.Cliente.Select(item => item.IdCliente).FirstAsync();
            var firstProductId = await check.Producto.Select(item => item.IdProducto).FirstAsync();
            var quoteForm = await httpClient.GetStringAsync("/Cotizacions/Create");
            var printToken = WebUtility.HtmlDecode(Regex.Match(quoteForm, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            Assert.False(string.IsNullOrEmpty(printToken));
            Task<HttpResponseMessage> Emitir() => httpClient.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ClienteId"] = firstClientId.ToString(), ["Lineas[0].ProductoId"] = firstProductId.ToString(),
                ["Lineas[0].Cantidad"] = "1", ["Lineas[0].PrecioUnitario"] = "10",
                ["__RequestVerificationToken"] = printToken
            }));
            var emitted = await Task.WhenAll(Emitir(), Emitir());
            Assert.All(emitted, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
            var numbered = await check.Cotizacion.AsNoTracking().Where(item => item.SerieInterna == "C001").ToListAsync();
            Assert.Equal(2, numbered.Count);
            Assert.Equal(2, numbered.Select(item => item.CorrelativoInterno).Distinct().Count());
            Assert.Equal(new[] { 4532, 4533 }, numbered.Select(item => item.CorrelativoInterno!.Value).OrderBy(item => item));
            Assert.Equal(4534, await check.SeriesDocumentoInterno.Where(item => item.TipoDocumento == "Cotizacion" && item.Activa)
                .Select(item => item.SiguienteCorrelativo).SingleAsync());

            var seriesForm = await httpClient.GetStringAsync("/SeriesInternas/Create");
            Assert.Contains("Próximo correlativo", seriesForm);
            var seriesToken = WebUtility.HtmlDecode(Regex.Match(seriesForm, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            var activate = await httpClient.PostAsync("/SeriesInternas/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TipoDocumento"] = "Cotizacion", ["Serie"] = "C002", ["SiguienteCorrelativo"] = "9000",
                ["Digitos"] = "4", ["Activa"] = "true", ["__RequestVerificationToken"] = seriesToken
            }));
            Assert.Equal(HttpStatusCode.Redirect, activate.StatusCode);
            Assert.Equal("C002", await check.SeriesDocumentoInterno.Where(item => item.TipoDocumento == "Cotizacion" && item.Activa)
                .Select(item => item.Serie).SingleAsync());
            var old = await check.SeriesDocumentoInterno.AsNoTracking().SingleAsync(item => item.TipoDocumento == "Cotizacion" && item.Serie == "C001");
            var rewind = await httpClient.PostAsync($"/SeriesInternas/Guardar/{old.IdSerieDocumentoInterno}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TipoDocumento"] = "Cotizacion", ["Serie"] = "C001", ["SiguienteCorrelativo"] = "4532",
                ["Digitos"] = "6", ["Activa"] = "false", ["__RequestVerificationToken"] = seriesToken
            }));
            Assert.Equal(HttpStatusCode.OK, rewind.StatusCode);
            Assert.Contains("debe superar", await rewind.Content.ReadAsStringAsync());

            var nextQuote = await httpClient.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ClienteId"] = firstClientId.ToString(), ["Lineas[0].ProductoId"] = firstProductId.ToString(),
                ["Lineas[0].Cantidad"] = "1", ["Lineas[0].PrecioUnitario"] = "10", ["__RequestVerificationToken"] = printToken
            }));
            Assert.Equal(HttpStatusCode.Redirect, nextQuote.StatusCode);
            var newer = await check.Cotizacion.AsNoTracking().SingleAsync(item => item.SerieInterna == "C002");
            Assert.Equal("C002-9000", newer.NumeroInterno);
            Assert.Equal(2, await check.Cotizacion.CountAsync(item => item.SerieInterna == "C001"));
            var reactivate = await httpClient.PostAsync($"/SeriesInternas/Guardar/{old.IdSerieDocumentoInterno}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TipoDocumento"] = "Cotizacion", ["Serie"] = "C001", ["SiguienteCorrelativo"] = "4534",
                ["Digitos"] = "6", ["Activa"] = "true", ["__RequestVerificationToken"] = seriesToken
            }));
            Assert.Equal(HttpStatusCode.Redirect, reactivate.StatusCode);
            Assert.Equal("C001", await check.SeriesDocumentoInterno.AsNoTracking().Where(item => item.TipoDocumento == "Cotizacion" && item.Activa)
                .Select(item => item.Serie).SingleAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var drop = new SqlCommand($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]", master);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
