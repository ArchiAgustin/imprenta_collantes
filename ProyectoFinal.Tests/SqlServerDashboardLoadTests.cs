using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;
using Xunit.Abstractions;

namespace ProyectoFinal.Tests;

public sealed class SqlServerDashboardLoadTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AuthenticatedDashboardStaysBelowOnePointFiveSecondsAtRepresentativeVolume()
    {
        var masterConnection = Environment.GetEnvironmentVariable("PROYECTOFINAL_SQL_LOAD_CONNECTION");
        if (string.IsNullOrWhiteSpace(masterConnection))
        {
            output.WriteLine("Carga no ejecutada: falta PROYECTOFINAL_SQL_LOAD_CONNECTION.");
            return;
        }
        var name = "ProyectoFinal_Load_" + Guid.NewGuid().ToString("N");
        var masterBuilder = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = "master" };
        var databaseBuilder = new SqlConnectionStringBuilder(masterBuilder.ConnectionString) { InitialCatalog = name };
        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync();
        await using (var create = new SqlCommand($"CREATE DATABASE [{name}]", master))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(databaseBuilder.ConnectionString).Options;
            int clientId;
            int productId;
            await using (var seed = new ApplicationDbContext(options))
            {
                await seed.Database.MigrateAsync();
                var client = new Cliente { Nombre = "Carga sintética BI" };
                var product = new Producto
                {
                    Nombre = "Servicio sintético BI", EsServicio = true, Precio = 150m,
                    CostoUnitarioSinIgv = 100m, Stock = 0
                };
                seed.Cliente.Add(client);
                seed.Producto.Add(product);
                await seed.SaveChangesAsync();
                clientId = client.IdCliente;
                productId = product.IdProducto;
                Assert.True(await new TramosDescuentoService(seed).AgregarAsync(productId, 100, 30m));
                var line = new CotizacionLineaInputViewModel { ProductoId = productId, Cantidad = 100, PrecioUnitario = 140m };
                var reference = Assert.Single(await new SugerenciaPrecioService(seed).ObtenerAsync(clientId, new[] { line }));
                Assert.Equal(111.12m, reference.PrecioRecomendadoSinIgv);
                Assert.Equal(100, reference.CantidadMinimaTramo);
                Assert.Equal(140m, line.PrecioUnitario);
            }
            await using (var db = new SqlConnection(databaseBuilder.ConnectionString))
            {
                await db.OpenAsync();
                const string sql = """
                    ;WITH n AS (SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY a.object_id, b.object_id) AS k
                        FROM sys.all_objects a CROSS JOIN sys.all_objects b)
                    INSERT INTO Cotizacion (idCliente, fechaCreacion, fechaVencimiento, total, estado)
                    SELECT @client, DATEADD(day, -(k % 366), CONVERT(date, GETDATE())),
                        DATEADD(day, 30 - (k % 366), CONVERT(date, GETDATE())), 35.40, N'Convertida' FROM n;
                    INSERT INTO Venta (idCotizacion, idCliente, fechaVenta, estado, total,
                        subtotalSinIgvEmision, igvEmision)
                    SELECT idCotizacion, @client,
                        TODATETIMEOFFSET(DATEADD(day, -(idCotizacion % 366), CONVERT(datetime2, GETDATE())), '+00:00'),
                        N'Confirmada', 35.40, 30.00, 5.40 FROM Cotizacion;
                    INSERT INTO DetalleVenta (idVenta, idProducto, productoNombre, cantidad,
                        precioUnitario, costoUnitarioSinIgv, esServicio, factorPresentacion)
                    SELECT v.idVenta, @product, N'Servicio sintético BI', 1, 10.00, 5.00, 1, 1
                    FROM Venta v CROSS JOIN (VALUES (1), (2), (3)) lineas(n);
                    """;
                await using var insert = new SqlCommand(sql, db) { CommandTimeout = 180 };
                insert.Parameters.AddWithValue("@client", clientId);
                insert.Parameters.AddWithValue("@product", productId);
                await insert.ExecuteNonQueryAsync();
            }
            await using (var verify = new ApplicationDbContext(options))
            {
                Assert.Equal(10000, await verify.Venta.CountAsync());
                Assert.Equal(30000, await verify.DetalleVenta.CountAsync());
            }
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = databaseBuilder.ConnectionString,
                    ["BootstrapAdmin:Email"] = "", ["BootstrapAdmin:Password"] = ""
                })));
            using var clientHttp = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using (var scope = factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var admin = new IdentityUser { UserName = "load-admin@example.test", Email = "load-admin@example.test" };
                Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
                Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
            }
            var login = await clientHttp.GetStringAsync("/Identity/Account/Login");
            var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success);
            var signedIn = await clientHttp.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = "load-admin@example.test", ["Input.Password"] = "TestPassword123!",
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
            }));
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            var quoteForm = await clientHttp.GetStringAsync("/Cotizacions/Create");
            var quoteToken = Regex.Match(quoteForm, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(quoteToken.Success);
            var reviewed = await clientHttp.PostAsync("/Cotizacions/RevisarPrecio", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ClienteId"] = clientId.ToString(), ["Lineas[0].ProductoId"] = productId.ToString(),
                ["Lineas[0].Cantidad"] = "100", ["Lineas[0].PrecioUnitario"] = "140",
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(quoteToken.Groups[1].Value)
            }));
            Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
            var reviewHtml = await reviewed.Content.ReadAsStringAsync();
            Assert.Contains("Precio recomendado", reviewHtml);
            Assert.True(reviewHtml.Contains("111,12") || reviewHtml.Contains("111.12"));
            Assert.Contains("El vendedor decide", reviewHtml);
            var today = FechaComercial.HoyPeru();
            var url = $"/Analytics?desde={today.AddDays(-365):yyyy-MM-dd}&hasta={today:yyyy-MM-dd}&umbralStock=5";
            for (var i = 0; i < 3; i++)
                Assert.Equal(HttpStatusCode.OK, (await clientHttp.GetAsync(url)).StatusCode);
            var times = new List<double>();
            for (var i = 0; i < 10; i++)
            {
                var timer = Stopwatch.StartNew();
                using var response = await clientHttp.GetAsync(url);
                _ = await response.Content.ReadAsByteArrayAsync();
                timer.Stop();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                times.Add(timer.Elapsed.TotalMilliseconds);
            }
            output.WriteLine($"SQL Server: {master.DataSource}; volumen: 10000 ventas, 30000 líneas; rango: 366 días; tiempos HTTP (ms): {string.Join(", ", times.Select(item => item.ToString("0.0")))}");
            Assert.All(times, time => Assert.True(time < 1500, $"/Analytics tardó {time:0.0} ms."));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var drop = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", master)
            { CommandTimeout = 180 };
            await drop.ExecuteNonQueryAsync();
        }
    }
}
