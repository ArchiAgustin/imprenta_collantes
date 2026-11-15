using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class NavigationTests
{
    [Fact]
    public async Task ManualPriceReviewShowsRecommendationWithoutSavingQuote()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        int clientId, productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customer = new Cliente { Nombre = "Cliente asesoría" };
            var product = new Producto { Nombre = "Volantes", Precio = 150m, CostoUnitarioSinIgv = 100m, EsServicio = true };
            db.AddRange(customer, product);
            await db.SaveChangesAsync();
            clientId = customer.IdCliente;
            productId = product.IdProducto;
            db.TramosDescuentoProducto.Add(new TramoDescuentoProducto
                { IdProducto = productId, CantidadMinima = 10, DescuentoPorcentaje = 5m });
            await db.SaveChangesAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "manual-price@example.test", Email = "manual-price@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
        }
        await Login(client, "manual-price@example.test");
        var page = await client.GetStringAsync("/Cotizacions/Create");
        var response = await client.PostAsync("/Cotizacions/RevisarPrecio", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["ClienteId"] = clientId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].Cantidad"] = "10",
            ["Lineas[0].PrecioUnitario"] = "145"
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Precio recomendado", html);
        using var verify = factory.Services.CreateScope();
        Assert.Empty(await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cotizacion.ToListAsync());
    }
    [Fact]
    public async Task OnlyAdminCanConfigureProductDiscountTiers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = new Producto { Nombre = "Producto con tramos", Precio = 100m };
            db.Producto.Add(product);
            await db.SaveChangesAsync();
            productId = product.IdProducto;
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "tier-admin@example.test", Email = "tier-admin@example.test" };
            var seller = new IdentityUser { UserName = "tier-seller@example.test", Email = "tier-seller@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
            Assert.True((await users.CreateAsync(seller, "TestPassword123!")).Succeeded);
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole("Vendedor"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(seller, "Vendedor")).Succeeded);
        }
        var path = $"/TramosDescuento?productoId={productId}";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(path)).StatusCode);
        await Login(client, "tier-seller@example.test");
        var denied = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.ToString());
        await client.GetAsync("/Identity/Account/Logout");
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(adminClient, "tier-admin@example.test");
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync(path)).StatusCode);
    }
    private static WebApplicationFactory<Program> CreateFactory(ISolicitudExtractor? extractor = null,
        ICorreoComercialSender? correo = null, IEmailSender? emailSender = null, TimeSpan? resetTokenLifespan = null)
    {
        var database = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "ProyectoFinal.Tests.Keys", Environment.ProcessId.ToString()));
            builder.ConfigureLogging(logging => logging.ClearProviders().AddDebug());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=Unused;TrustServerCertificate=True",
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "ProyectoFinal.Tests.Keys", Environment.ProcessId.ToString()),
                ["BootstrapAdmin:Email"] = "",
                ["BootstrapAdmin:Password"] = ""
            }));
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<KeyManagementOptions>(options => options.XmlEncryptor = null);
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(database));
                if (extractor is not null)
                {
                    services.RemoveAll<ISolicitudExtractor>();
                    services.AddSingleton(extractor);
                }
                if (correo is not null)
                {
                    services.RemoveAll<ICorreoComercialSender>();
                    services.AddSingleton(correo);
                }
                if (emailSender is not null)
                {
                    services.RemoveAll<IEmailSender>();
                    services.AddSingleton(emailSender);
                }
                if (resetTokenLifespan is { } lifespan)
                    services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = lifespan);
            });
        });
    }

    [Theory]
    [InlineData("/Clientes")]
    [InlineData("/Proveedors")]
    [InlineData("/Productoes")]
    [InlineData("/Cotizacions")]
    [InlineData("/Ventas")]
    [InlineData("/Taller")]
    [InlineData("/Analytics")]
    [InlineData("/Analytics/ExportarExcel")]
    [InlineData("/Analytics/ExportarPdf")]
    [InlineData("/PreCotizacion")]
    [InlineData("/SeriesInternas")]
    [InlineData("/Admin")]
    [InlineData("/Roles")]
    [InlineData("/Identity/Usuario/Panel")]
    public async Task AnonymousUserIsSentToLogin(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect,
            $"Expected redirect, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AdminCanOpenPanelAndModules()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "admin@example.test", Email = "admin@example.test" };
            Assert.True((await manager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);

            // La analítica solo renderiza gráfico y tabla cuando hay ventas confirmadas en el periodo.
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cliente = new Cliente { Nombre = "Cliente de navegación" };
            var producto = new Producto { Nombre = "Artículo de navegación", EsServicio = false };
            db.AddRange(cliente, producto);
            await db.SaveChangesAsync();
            var venta = new Venta
            {
                IdCliente = cliente.IdCliente,
                FechaVentaUtc = DateTimeOffset.UtcNow,
                Total = 47.20m,
                Estado = EstadoVenta.Confirmada
            };
            venta.Detalles.Add(new DetalleVenta
            {
                IdProducto = producto.IdProducto,
                ProductoNombre = producto.Nombre,
                Cantidad = 1,
                PrecioUnitario = 40m,
                FactorPresentacion = 1m
            });
            db.Venta.Add(venta);
            // El embudo de conversión solo se dibuja cuando hay cotizaciones emitidas en el periodo.
            var hoy = FechaComercial.HoyPeru();
            db.Cotizacion.Add(new Cotizacion
            {
                IdCliente = cliente.IdCliente,
                FechaCreacion = hoy,
                FechaVencimiento = hoy.AddDays(15),
                Estado = "Emitida",
                Total = 40m
            });
            await db.SaveChangesAsync();
        }
        await Login(client, "admin@example.test");

        var panel = await client.GetAsync("/Identity/Usuario/Panel");
        Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
        var panelHtml = WebUtility.HtmlDecode(await panel.Content.ReadAsStringAsync());
        Assert.Contains("Clientes", panelHtml);
        Assert.Contains("Series", panelHtml);
        Assert.DoesNotContain("Series internas", panelHtml);
        Assert.Contains("Catálogos SUNAT", panelHtml);
        Assert.Contains("Almacenes", panelHtml);
        Assert.Contains("Inventario", panelHtml);
        Assert.Contains("Productos", panelHtml);
        Assert.Contains("Kárdex reciente", panelHtml);
        Assert.Contains("Transferencias", panelHtml);
        Assert.DoesNotContain("Conteo físico en curso", panelHtml);
        Assert.Contains("Taller", panelHtml);
        Assert.DoesNotContain("collapse show", panelHtml);
        Assert.True(panelHtml.IndexOf("Administración", StringComparison.Ordinal) < panelHtml.IndexOf("Catálogos SUNAT", StringComparison.Ordinal));
        Assert.True(panelHtml.IndexOf("Catálogos SUNAT", StringComparison.Ordinal) < panelHtml.IndexOf("Almacén", StringComparison.Ordinal));
        Assert.True(panelHtml.IndexOf("Almacén", StringComparison.Ordinal) < panelHtml.IndexOf("Comercial", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
        Assert.Contains("/Identity/Usuario/Panel", (await client.GetAsync("/")).Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/SeriesInternas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/CatalogosSunat")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventario")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventario/Kardex")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Inventario/Conteos")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Inventario/Cortes")).StatusCode);
        Assert.DoesNotContain("Administrar almacenes", await client.GetStringAsync("/Inventario"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
        var users = await client.GetStringAsync("/Admin");
        Assert.Contains("Roles asignados", users);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Roles")).StatusCode);
        var permissions = await client.GetStringAsync("/Permisos");
        Assert.Contains("Permisos por rol", permissions);
        var audit = await client.GetStringAsync("/Auditoria");
        Assert.Contains("Historial de auditoría", audit);
        var analytics = await client.GetStringAsync("/Analytics");
        Assert.Contains("Ventas confirmadas con IGV", analytics);
        Assert.Contains("No calculable", analytics);
        Assert.Contains("ventas-confirmadas-chart", analytics);
        Assert.Contains("embudo-conversion", analytics);
        Assert.Contains("Consultar datos en tabla", analytics);
        Assert.Contains("chart.js@4.4.8", analytics);
        var excel = await client.GetAsync("/Analytics/ExportarExcel?desde=2026-10-23&hasta=2026-10-24&umbralStock=5");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.Content.Headers.ContentType?.MediaType);
        await using (var excelStream = new MemoryStream(await excel.Content.ReadAsByteArrayAsync()))
        using (var workbook = new XLWorkbook(excelStream))
            Assert.Equal(new[] { "Resumen", "Tendencia", "Productos", "Clientes", "Taller", "Stock" },
                workbook.Worksheets.Select(sheet => sheet.Name).ToArray());
        var pdf = await client.GetAsync("/Analytics/ExportarPdf?desde=2026-10-23&hasta=2026-10-24&umbralStock=5");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Analytics/ExportarExcel?desde=2026-10-24&hasta=2026-10-23")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Analytics?desde=2026-10-24&hasta=2026-10-23")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Analytics?desde=2026-10-23&hasta=2026-10-24&umbralStock=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Identity/Account/Manage/IndexRole")).StatusCode);
        var account = await client.GetAsync("/Identity/Account/Manage");
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.Contains("Configuración de la cuenta", await account.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UserWithoutAdminRoleSeesPendingAccessNotTheCrud()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await manager.CreateAsync(new IdentityUser
            {
                UserName = "guest@example.test", Email = "guest@example.test"
            }, "TestPassword123!")).Succeeded);
        }
        var login = await Login(client, "guest@example.test");
        Assert.Equal("/Identity/Usuario/Panel", login.Headers.Location?.ToString());
        var panel = await client.GetStringAsync("/Identity/Usuario/Panel");
        Assert.Contains("Aún no tienes módulos asignados", panel);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/SeriesInternas")).StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateCustomerAndQuotationWithServerOwnedAuditFields()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "owner@example.test", Email = "owner@example.test" };
            Assert.True((await manager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
            userId = user.Id;

            var seedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seedContext.DocumentoIdentidadTipos.Add(new DocumentoIdentidadTipo
            {
                IdDocumentoIdentidadTipo = 2, Codigo = "1", Nombre = "Documento nacional de identidad", Abreviatura = "DNI", Estado = true, FechaCrea = DateTime.UtcNow
            });
            seedContext.Producto.Add(new Producto { Nombre = "Impresión de prueba", Precio = 100m, Stock = 10 });
            await seedContext.SaveChangesAsync();
        }
        await Login(client, "owner@example.test");
        var customerForm = await client.GetStringAsync("/Clientes/Create");
        var customerResponse = await client.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Cliente de prueba",
            ["IdDocumentoIdentidadTipo"] = "2",
            ["NumeroDocumento"] = "12345678",
            ["__RequestVerificationToken"] = Token(customerForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, customerResponse.StatusCode);
        var customers = await client.GetStringAsync("/Clientes");
        Assert.Contains("Cliente de prueba", customers);

        using var scopeForId = factory.Services.CreateScope();
        var context = scopeForId.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customerId = context.Cliente.Single().IdCliente;
        var productId = context.Producto.Single().IdProducto;
        var quoteForm = await client.GetStringAsync("/Cotizacions/Create");
        var quoteResponse = await client.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = customerId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].Cantidad"] = "2",
            ["Lineas[0].PrecioUnitario"] = "100",
            ["Descripcion"] = "Trabajo de prueba",
            ["IdUsuario"] = "forged-id",
            ["FechaCreacion"] = "2000-01-01",
            ["Total"] = "1",
            ["__RequestVerificationToken"] = Token(quoteForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, quoteResponse.StatusCode);
        var quote = context.Cotizacion.Single();
        Assert.Equal(userId, quote.IdUsuario);
        Assert.Equal(FechaComercial.HoyPeru(), quote.FechaCreacion);
        Assert.Equal(236m, quote.Total);
        var issuedAudit = await context.AuditoriaOperacion.AsNoTracking()
            .SingleAsync(item => item.TipoEntidad == "Cotizacion" && item.IdEntidad == quote.IdCotizacion
                && item.Accion == "Cotización emitida");
        Assert.Equal(userId, issuedAudit.IdUsuario);
        Assert.Null(issuedAudit.EstadoAnterior);
        Assert.Equal("Emitida", issuedAudit.EstadoNuevo);
        Assert.Equal(TimeSpan.Zero, issuedAudit.FechaUtc.Offset);
        var quoteLine = context.DetalleCotizacion.Single();
        Assert.Equal(productId, quoteLine.IdProducto);
        Assert.Equal(2, quoteLine.Cantidad);
        Assert.Equal(100m, quoteLine.PrecioUnitario);
        var details = await client.GetAsync($"/Cotizacions/Details/{quote.IdCotizacion}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsHtml = await details.Content.ReadAsStringAsync();
        var savedProductName = await context.DetalleCotizacion
            .AsNoTracking()
            .Include(line => line.IdProductoNavigation)
            .Select(line => line.IdProductoNavigation!.Nombre)
            .SingleAsync();
        Assert.Equal("Impresión de prueba", savedProductName);
        Assert.Contains("Impresión de prueba", WebUtility.HtmlDecode(detailsHtml));
        Assert.Contains("IGV (18%)", detailsHtml);

        var editHtml = await client.GetStringAsync($"/Cotizacions/Edit/{quote.IdCotizacion}");
        var editResponse = await client.PostAsync($"/Cotizacions/Edit/{quote.IdCotizacion}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdCotizacion"] = quote.IdCotizacion.ToString(),
            ["ClienteId"] = customerId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].Cantidad"] = "3",
            ["Lineas[0].PrecioUnitario"] = "200",
            ["Descripcion"] = "Edición de prueba",
            ["__RequestVerificationToken"] = Token(editHtml)
        }));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        var updatedQuote = await context.Cotizacion.AsNoTracking().SingleAsync(item => item.IdCotizacion == quote.IdCotizacion);
        Assert.Equal(708m, updatedQuote.Total);
        var updatedLine = await context.DetalleCotizacion.AsNoTracking().SingleAsync();
        Assert.Equal(3, updatedLine.Cantidad);
        Assert.Equal(200m, updatedLine.PrecioUnitario);
        var editedAudit = await context.AuditoriaOperacion.AsNoTracking()
            .SingleAsync(item => item.TipoEntidad == "Cotizacion" && item.IdEntidad == quote.IdCotizacion
                && item.Accion == "Cotización actualizada");
        Assert.Equal(userId, editedAudit.IdUsuario);
        Assert.Equal("Emitida", editedAudit.EstadoAnterior);
        Assert.Equal("Emitida", editedAudit.EstadoNuevo);
    }

    [Fact]
    public async Task CustomerContactsAreValidatedAndSearchIncludesLegacyIdentity()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "customer-search@example.test", Email = "customer-search@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DocumentoIdentidadTipos.Add(new DocumentoIdentidadTipo
            {
                IdDocumentoIdentidadTipo = 2, Codigo = "1", Nombre = "DNI", Abreviatura = "DNI", Estado = true
            });
            db.Cliente.AddRange(
                new Cliente
                {
                    Nombre = "Ana Muestra", Correo = "ana@example.test", Telefono = "+51 987 654 321",
                    IdDocumentoIdentidadTipo = 2, NumeroDocumento = "12345678", Dni = "12345678"
                },
                new Cliente { Nombre = "Cliente Legado", Dni = "87654321" });
            await db.SaveChangesAsync();
        }

        await Login(client, "customer-search@example.test");
        var form = await client.GetStringAsync("/Clientes/Create");
        var invalid = await client.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Contacto inválido", ["Correo"] = "correo-invalido", ["Telefono"] = "abc",
            ["IdDocumentoIdentidadTipo"] = "2", ["NumeroDocumento"] = "11112222",
            ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync());
        Assert.Contains("correo electrónico válido", invalidHtml);
        Assert.Contains("teléfono válido", invalidHtml);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cliente.CountAsync());

        var byEmail = await client.GetStringAsync("/Clientes?correo=ana%40example.test");
        Assert.Contains("Ana Muestra", byEmail);
        Assert.DoesNotContain("Cliente Legado", byEmail);
        Assert.Contains("ana@example.test", byEmail);

        var byPhone = await client.GetStringAsync("/Clientes?telefono=987%20654");
        Assert.Contains("Ana Muestra", byPhone);
        var byLegacyDocument = await client.GetStringAsync("/Clientes?documento=87654321");
        Assert.Contains("Cliente Legado", byLegacyDocument);
        Assert.Contains("value=\"87654321\"", byLegacyDocument);
        var byName = await client.GetStringAsync("/Clientes?nombre=Ana");
        Assert.Contains("Ana Muestra", byName);
        Assert.DoesNotContain("Cliente Legado", byName);
    }

    [Fact]
    public async Task CustomerCommercialFichaShowsCompleteHistoryAndExcludesCancelledSalesFromTotal()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "ficha-cliente@example.test";
        int customerId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customer = new Cliente { Nombre = "Ficha Muestra", Correo = "ficha@example.test" };
            db.Cliente.Add(customer);
            await db.SaveChangesAsync();
            customerId = customer.IdCliente;

            var quote = new Cotizacion
            {
                IdCliente = customerId, IdUsuario = admin.Id, SerieInterna = "C123", CorrelativoInterno = 42,
                DigitosNumeroInterno = 6, FechaCreacion = new DateOnly(2026, 4, 2),
                FechaVencimiento = new DateOnly(2026, 4, 9), Estado = "Convertida", Total = 118m
            };
            var cancelledQuote = new Cotizacion
            {
                IdCliente = customerId, IdUsuario = admin.Id, SerieInterna = "C123", CorrelativoInterno = 43,
                DigitosNumeroInterno = 6, FechaCreacion = new DateOnly(2026, 4, 3),
                Estado = "Convertida", Total = 999m
            };
            db.Cotizacion.AddRange(quote, cancelledQuote);
            await db.SaveChangesAsync();

            var sale = new Venta
            {
                IdCotizacion = quote.IdCotizacion, IdCliente = customerId, IdUsuario = admin.Id,
                SerieInterna = "V321", CorrelativoInterno = 7, DigitosNumeroInterno = 6,
                FechaVentaUtc = DateTimeOffset.UtcNow, Estado = EstadoVenta.Confirmada, Total = 118m
            };
            var cancelledSale = new Venta
            {
                IdCotizacion = cancelledQuote.IdCotizacion, IdCliente = customerId, IdUsuario = admin.Id,
                FechaVentaUtc = DateTimeOffset.UtcNow, Estado = EstadoVenta.Cancelada, Total = 999m
            };
            db.Venta.AddRange(sale, cancelledSale);
            await db.SaveChangesAsync();
            db.Pedido.Add(new Pedido
            {
                IdCliente = customerId, IdCotizacion = quote.IdCotizacion, IdVenta = sale.IdVenta,
                IdUsuario = admin.Id, FechaPedido = new DateOnly(2026, 4, 2),
                EstadoPedido = EstadoPedido.EnProduccion, Total = 118m
            });
            await db.SaveChangesAsync();
        }

        await Login(client, email);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Clientes/Details/{customerId}"));
        Assert.Contains("Ficha comercial", html);
        Assert.Contains("Ventas confirmadas con IGV", html);
        Assert.Contains("118.00", html);
        Assert.DoesNotContain("S/ 1,117.00", html);
        Assert.Contains("C123-000042", html);
        Assert.Contains("Convertida", html);
        Assert.Contains("V321-000007", html);
        Assert.Contains("En impresión / acabado", html);
        Assert.Contains("no representa facturación tributaria", html);
    }

    [Fact]
    public async Task SupplierDirectoryCanSearchAndFichaListsAssociatedProducts()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "ficha-proveedor@example.test";
        int supplierId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DocumentoIdentidadTipos.Add(new DocumentoIdentidadTipo
            {
                IdDocumentoIdentidadTipo = 4, Codigo = "6", Nombre = "RUC", Abreviatura = "RUC", Estado = true
            });
            var supplier = new Proveedor
            {
                Nombre = "Insumos del Centro", Contacto = "María Compras", Ruc = "20123456789",
                IdDocumentoIdentidadTipo = 4, NumeroDocumento = "20123456789"
            };
            var product = new Producto { Nombre = "Papel couché", Precio = 25m, EsServicio = false };
            supplier.Productos.Add(product);
            db.Proveedor.Add(supplier);
            await db.SaveChangesAsync();
            supplierId = supplier.IdProveedor;
        }

        await Login(client, email);
        var byDocument = WebUtility.HtmlDecode(await client.GetStringAsync("/Proveedors?documento=20123456789"));
        Assert.Contains("Insumos del Centro", byDocument);
        Assert.DoesNotContain("No hay proveedores que coincidan", byDocument);
        var byContact = WebUtility.HtmlDecode(await client.GetStringAsync("/Proveedors?contacto=María"));
        Assert.Contains("Insumos del Centro", byContact);
        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/Proveedors/Details/{supplierId}"));
        Assert.Contains("Productos asociados", details);
        Assert.Contains("Papel couché", details);
        Assert.Contains("Artículo", details);
        Assert.Contains("Ver producto", details);
    }

    [Fact]
    public async Task ArticlesRequireAnExplicitBaseUnitAndCanHaveManagedPresentations()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "unit-admin@example.test";
        int baseUnitId;
        int packageUnitId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var baseUnit = new UnidadMedida { Descripcion = "Pliego", DescripcionCorta = "plg", Activo = true };
            var packageUnit = new UnidadMedida { Descripcion = "Paquete", DescripcionCorta = "paq", Activo = true };
            db.UnidadesMedida.AddRange(baseUnit, packageUnit);
            await db.SaveChangesAsync();
            baseUnitId = baseUnit.IdUnidadMedida;
            packageUnitId = packageUnit.IdUnidadMedida;
        }

        await Login(client, email);
        var form = await client.GetStringAsync("/Productoes/Create");
        Assert.Contains("Unidad base del artículo", form);
        var missingUnit = await client.PostAsync("/Productoes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Artículo sin unidad", ["Precio"] = "2.50", ["EsServicio"] = "false",
            ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.OK, missingUnit.StatusCode);
        Assert.Contains("unidad base activa y revisada", WebUtility.HtmlDecode(await missingUnit.Content.ReadAsStringAsync()));

        var validForm = await client.GetStringAsync("/Productoes/Create");
        var created = await client.PostAsync("/Productoes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Cartulina", ["Precio"] = "2.50", ["EsServicio"] = "false",
            ["IdUnidadMedidaBase"] = baseUnitId.ToString(),
            ["__RequestVerificationToken"] = Token(validForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Producto.SingleAsync();
            productId = product.IdProducto;
            Assert.Equal(baseUnitId, product.IdUnidadMedidaBase);
            var basePresentation = await db.PresentacionesProducto.SingleAsync();
            Assert.Equal(baseUnitId, basePresentation.IdUnidadMedida);
            Assert.Equal(1m, basePresentation.Factor);
        }

        var details = await client.GetStringAsync($"/Productoes/Details/{productId}");
        Assert.Contains("Presentaciones", details);
        var added = await client.PostAsync($"/Productoes/AgregarPresentacion/{productId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdUnidadMedida"] = packageUnitId.ToString(),
            ["Descripcion"] = "Paquete de 10 pliegos",
            ["Factor"] = "10",
            ["__RequestVerificationToken"] = Token(details)
        }));
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var package = await verify.PresentacionesProducto.SingleAsync(item => item.Descripcion == "Paquete de 10 pliegos");
        Assert.Equal(10m, package.Factor);
        Assert.Equal(productId, package.IdProducto);
        Assert.Contains(await verify.AuditoriaOperacion.ToListAsync(), item => item.Accion == "Presentación creada");
    }

    [Fact]
    public async Task QuotePresentationFactorPricesPerPackAndMovesBaseStockOnConfirmAndCancel()
    {
        var emailSender = new CapturaCorreoComercialSender();
        using var factory = CreateFactory(correo: emailSender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "quote-pack-admin@example.test";
        string userId;
        int customerId, productId, warehouseId, packPresentationId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
            userId = admin.Id;

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customer = new Cliente { Nombre = "Cliente por paquete", Correo = "cliente-paquete@example.test" };
            var unit = new UnidadMedida { Descripcion = "Pliego", DescripcionCorta = "plg", Activo = true };
            var packageUnit = new UnidadMedida { Descripcion = "Paquete", DescripcionCorta = "paq", Activo = true };
            var warehouse = new Almacen
            {
                Nombre = InventarioService.NombreAlmacen, EsPredeterminado = true,
                Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow
            };
            db.AddRange(customer, unit, packageUnit, warehouse);
            await db.SaveChangesAsync();
            customerId = customer.IdCliente;
            warehouseId = warehouse.IdAlmacen;

            var product = new Producto
            {
                Nombre = "Pliego especial", Precio = 10m, CostoUnitarioSinIgv = 1m,
                EsServicio = false, IdUnidadMedidaBase = unit.IdUnidadMedida
            };
            db.Producto.Add(product);
            await db.SaveChangesAsync();
            productId = product.IdProducto;
            db.PresentacionesProducto.AddRange(
                new PresentacionProducto
                {
                    IdProducto = productId, IdUnidadMedida = unit.IdUnidadMedida,
                    Descripcion = "Pliego", Factor = 1m, Activo = true
                },
                new PresentacionProducto
                {
                    IdProducto = productId, IdUnidadMedida = packageUnit.IdUnidadMedida,
                    Descripcion = "Paquete de 10 pliegos", Factor = 10m, Activo = true
                });
            await db.SaveChangesAsync();
            packPresentationId = await db.PresentacionesProducto
                .Where(item => item.IdProducto == productId && item.Factor == 10m)
                .Select(item => item.IdPresentacionProducto).SingleAsync();
            db.SaldosAlmacen.Add(new StockAlmacen
            {
                IdAlmacen = warehouseId, IdProducto = productId, CantidadBase = 30m
            });
            await db.SaveChangesAsync();
        }

        await Login(client, email);
        var form = await client.GetStringAsync("/Cotizacions/Create");
        Assert.Contains("Paquete de 10 pliegos", form);
        var belowPresentationFloor = await client.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = customerId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].IdPresentacionProducto"] = packPresentationId.ToString(),
            ["Lineas[0].FactorPresentacion"] = "1",
            ["Lineas[0].Cantidad"] = "2",
            ["Lineas[0].PrecioUnitario"] = "10",
            ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.OK, belowPresentationFloor.StatusCode);
        Assert.Contains("margen mínimo del 10 %", WebUtility.HtmlDecode(await belowPresentationFloor.Content.ReadAsStringAsync()));
        using (var scope = factory.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cotizacion.ToListAsync());

        var quoteResponse = await client.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = customerId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].IdPresentacionProducto"] = packPresentationId.ToString(),
            ["Lineas[0].FactorPresentacion"] = "1", // Untrusted: the server must use the catalog factor.
            ["Lineas[0].Cantidad"] = "2",
            ["Lineas[0].PrecioUnitario"] = "23",
            ["Total"] = "1",
            ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.Redirect, quoteResponse.StatusCode);

        int quoteId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var quote = await db.Cotizacion.Include(item => item.DetalleCotizacions).SingleAsync();
            quoteId = quote.IdCotizacion;
            Assert.Equal(54.28m, quote.Total);
            var detail = Assert.Single(quote.DetalleCotizacions);
            Assert.Equal(packPresentationId, detail.IdPresentacionProducto);
            Assert.Equal(10m, detail.FactorPresentacion);
            Assert.Equal("Paquete de 10 pliegos", detail.PresentacionDescripcion);
        Assert.Equal("Pliego", detail.UnidadBaseDescripcion);
        }

        var quoteDetails = WebUtility.HtmlDecode(await client.GetStringAsync($"/Cotizacions/Details/{quoteId}"));
        Assert.Contains("2 × Paquete de 10 pliegos", quoteDetails);
        var pdfResponse = await client.GetAsync($"/Cotizacions/DescargarPdf/{quoteId}");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5));
        Assert.True(pdfBytes.Length > 1000);

        var sentEmail = await client.PostAsync("/Cotizacions/EnviarPorCorreo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = quoteId.ToString(), ["__RequestVerificationToken"] = Token(quoteDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, sentEmail.StatusCode);
        Assert.Equal("cliente-paquete@example.test", emailSender.Recipient);
        Assert.Contains("Cotización", emailSender.Subject);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(emailSender.Attachment!, 0, 5));

        var confirmed = await client.PostAsync("/Ventas/ConfirmarDesdeCotizacion", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cotizacionId"] = quoteId.ToString(), ["idAlmacen"] = warehouseId.ToString(),
            ["__RequestVerificationToken"] = Token(quoteDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);

        int saleId;
        int orderId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Venta.Include(item => item.Detalles).Include(item => item.Pedido).SingleAsync();
            saleId = sale.IdVenta;
            orderId = sale.Pedido!.IdPedido;
            var soldLine = Assert.Single(sale.Detalles);
            Assert.Equal(2, soldLine.Cantidad);
            Assert.Equal(10m, soldLine.FactorPresentacion);
            Assert.Equal(1m, soldLine.CostoUnitarioSinIgv);
            Assert.Equal(10m, (await db.SaldosAlmacen.FindAsync(warehouseId, productId))!.CantidadBase);
            var kardex = await db.Kardex.SingleAsync();
            Assert.Equal(20m, kardex.CantidadBase);
            Assert.Equal(30m, kardex.SaldoAnteriorBase);
            Assert.Equal(10m, kardex.SaldoPosteriorBase);
        }

        var saleDetails = await client.GetStringAsync($"/Ventas/Details/{saleId}");
        Assert.Contains("2 × Paquete de 10 pliegos", WebUtility.HtmlDecode(saleDetails));
        var cancelled = await client.PostAsync("/Ventas/Cancelar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = saleId.ToString(), ["__RequestVerificationToken"] = Token(saleDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(30m, (await db.SaldosAlmacen.FindAsync(warehouseId, productId))!.CantidadBase);
            var returnEntry = await db.Kardex.OrderBy(item => item.IdKardex).LastAsync();
            Assert.Equal(20m, returnEntry.CantidadBase);
            Assert.Equal(10m, returnEntry.SaldoAnteriorBase);
            Assert.Equal(30m, returnEntry.SaldoPosteriorBase);

            var customer = await db.Cliente.SingleAsync();
            var admin = await db.Users.SingleAsync(item => item.Id == userId);
            var insufficient = new Cotizacion
            {
                IdCliente = customer.IdCliente, IdUsuario = admin.Id,
                FechaCreacion = FechaComercial.HoyPeru(), Estado = "Emitida", Total = 108.56m
            };
            insufficient.DetalleCotizacions.Add(new DetalleCotizacion
            {
                IdProducto = productId, IdPresentacionProducto = packPresentationId,
                FactorPresentacion = 10m, PresentacionDescripcion = "Paquete de 10 pliegos",
                UnidadBaseDescripcion = "Pliego", Cantidad = 4, PrecioUnitario = 23m
            });
            db.Cotizacion.Add(insufficient);
            await db.SaveChangesAsync();
            var actor = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, admin.Id) }, "Test"));
            var rejected = await new VentaService(db).ConfirmarCotizacionAsync(insufficient.IdCotizacion, warehouseId, admin.Id, actor);
            Assert.False(rejected.Exito);
            Assert.Equal("Emitida", insufficient.Estado);
            Assert.Equal(30m, (await db.SaldosAlmacen.FindAsync(warehouseId, productId))!.CantidadBase);
            Assert.Single(await db.Venta.ToListAsync());
            Assert.Single(await db.Pedido.ToListAsync());
        }
        Assert.True(orderId > 0);
    }

    [Fact]
    public async Task ProductStockActionLoadsAStoreByStoreModal()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "stock-modal@example.test", Email = "stock-modal@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = new Producto { Nombre = "Papel", Precio = 10m, Stock = 0 };
            var mainWarehouse = new Almacen
            {
                Nombre = InventarioService.NombreAlmacen, Activo = true, EsPredeterminado = true,
                FechaActivacionUtc = DateTimeOffset.UtcNow
            };
            var secondWarehouse = new Almacen
            {
                Nombre = "Depósito", Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow
            };
            db.AddRange(product, mainWarehouse, secondWarehouse);
            await db.SaveChangesAsync();
            productId = product.IdProducto;
            db.SaldosAlmacen.AddRange(
                new StockAlmacen { IdAlmacen = mainWarehouse.IdAlmacen, IdProducto = productId, CantidadBase = 12m },
                new StockAlmacen { IdAlmacen = secondWarehouse.IdAlmacen, IdProducto = productId, CantidadBase = 4m });
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.Almacenes.CountAsync());
        }

        await Login(client, "stock-modal@example.test");
        var products = await client.GetStringAsync("/Productoes");
        Assert.Contains("Stock por almacén", products);
        var modal = WebUtility.HtmlDecode(await client.GetStringAsync($"/Productoes/StockPorAlmacen/{productId}"));
        Assert.Contains("Papel", modal);
        Assert.Contains("Almacén principal", modal);
        Assert.Contains("Depósito", modal);
        Assert.Contains("12", modal);
        Assert.Contains("4", modal);
    }

    [Fact]
    public async Task InventoryHeaderAndSelectedDetailsApplyWithoutPausingSales()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        int warehouseId, productId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "cut-admin@example.test", Email = "cut-admin@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouse = new Almacen
            {
                Nombre = InventarioService.NombreAlmacen,
                Activo = true,
                EsPredeterminado = true,
                FechaActivacionUtc = DateTimeOffset.UtcNow
            };
            var unit = new UnidadMedida { IdUnidadMedida = 1, Descripcion = "UNIDAD (BIENES)", DescripcionCorta = "UND", Activo = true };
            var product = new Producto { Nombre = "Cartulina", Precio = 5m, Stock = 0, IdUnidadMedidaBase = 1 };
            db.AddRange(warehouse, unit, product, new SerieInventario { Serie = "INV1", SiguienteCorrelativo = 2, Digitos = 8, Activa = true });
            await db.SaveChangesAsync();
            warehouseId = warehouse.IdAlmacen;
            productId = product.IdProducto;
            db.SaldosAlmacen.Add(new StockAlmacen
            {
                IdAlmacen = warehouseId, IdProducto = productId, CantidadBase = 10m
            });
            await db.SaveChangesAsync();
        }

        await Login(client, "cut-admin@example.test");
        var page = await client.GetStringAsync("/Inventario");
        Assert.Contains("Inventario", page);
        Assert.Contains("INV1-00000002", page);
        Assert.Contains("Nuevo", page);
        Assert.Contains("Coordina el conteo con tu equipo", page);
        Assert.DoesNotContain("Conteo físico en curso", page);
        var autocomplete = await client.GetStringAsync($"/Inventario/BuscarProductos?termino=Cart&almacenId={warehouseId}");
        Assert.Contains("Cartulina", autocomplete);
        var result = await client.PostAsync("/Inventario/Crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdAlmacen"] = warehouseId.ToString(),
            ["Fecha"] = FechaComercial.HoyPeru().ToString("yyyy-MM-dd"),
            ["Detalles[0].IdProducto"] = productId.ToString(),
            ["Detalles[0].CantidadBase"] = "4",
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4m, (await verify.SaldosAlmacen.FindAsync(warehouseId, productId))!.CantidadBase);
        var inventory = await verify.Inventarios.Include(item => item.Detalles).SingleAsync();
        Assert.Equal("INV1-00000002", inventory.Numero);
        Assert.Equal(FechaComercial.HoyPeru(), inventory.Fecha);
        Assert.Equal(warehouseId, inventory.IdAlmacen);
        Assert.Single(inventory.Detalles);
        Assert.False((await verify.Almacenes.FindAsync(warehouseId))!.ConteoEnCurso);
        var movement = await verify.Kardex.SingleAsync();
        Assert.Equal("Ajuste salida", movement.TipoMovimiento);
        Assert.Equal("Inventario", movement.TipoReferencia);
        Assert.Equal(10m, movement.SaldoAnteriorBase);
        Assert.Equal(4m, movement.SaldoPosteriorBase);
        Assert.Empty(await verify.CortesInventario.ToListAsync());
    }

    [Fact]
    public async Task InternalDocumentsDefaultSevenDaysAndRevalidateWithoutChangingOriginal()
    {
        using var factory = CreateFactory();
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        int customerId, productId, warehouseId;
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "print-admin@example.test", Email = "print-admin@example.test" };
            Assert.True((await manager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customer = new Cliente { Nombre = "Cliente inicial", NumeroDocumento = "12345678", Direccion = "Dirección de prueba" };
            var product = new Producto { Nombre = "Artículo", Stock = 5, Precio = 10, CostoUnitarioSinIgv = 4 };
            var warehouse = new Almacen { Nombre = InventarioService.NombreAlmacen, EsPredeterminado = true, Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow };
            db.AddRange(customer, product, warehouse);
            await db.SaveChangesAsync();
            db.SaldosAlmacen.Add(new StockAlmacen { IdAlmacen = warehouse.IdAlmacen, IdProducto = product.IdProducto, CantidadBase = 5m });
            await db.SaveChangesAsync();
            customerId = customer.IdCliente;
            productId = product.IdProducto;
            warehouseId = warehouse.IdAlmacen;
        }
        await Login(browser, "print-admin@example.test");
        var form = await browser.GetStringAsync("/Cotizacions/Create");
        Assert.Contains($"value=\"{FechaComercial.HoyPeru().AddDays(7):yyyy-MM-dd}\"", form);
        var posted = await browser.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = customerId.ToString(), ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].Cantidad"] = "2", ["Lineas[0].PrecioUnitario"] = "10",
            ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        int originalId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var quote = await db.Cotizacion.SingleAsync();
            originalId = quote.IdCotizacion;
            Assert.Equal("C001", quote.SerieInterna);
            Assert.Equal(4532, quote.CorrelativoInterno);
            Assert.Equal("C001-004532", quote.NumeroInterno);
            Assert.Equal(FechaComercial.HoyPeru().AddDays(7), quote.FechaVencimiento);
            quote.FechaVencimiento = FechaComercial.HoyPeru().AddDays(-1);
            await db.SaveChangesAsync();
        }
        var revalidate = await browser.GetStringAsync($"/Cotizacions/Revalidar/{originalId}");
        Assert.Contains("Revisa cliente", revalidate);
        var replacement = await browser.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdCotizacionOrigen"] = originalId.ToString(), ["ClienteId"] = customerId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(), ["Lineas[0].Cantidad"] = "2",
            ["Lineas[0].PrecioUnitario"] = "10", ["__RequestVerificationToken"] = Token(revalidate)
        }));
        Assert.Equal(HttpStatusCode.Redirect, replacement.StatusCode);
        int newId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var original = await db.Cotizacion.SingleAsync(q => q.IdCotizacion == originalId);
            Assert.Equal("Vencida", original.Estado);
            Assert.Equal(2, await db.Cotizacion.CountAsync());
            var issued = await db.Cotizacion.SingleAsync(q => q.IdCotizacionOrigen == originalId);
            newId = issued.IdCotizacion;
            Assert.NotEqual(original.CorrelativoInterno, issued.CorrelativoInterno);
            Assert.Equal(FechaComercial.HoyPeru().AddDays(7), issued.FechaVencimiento);
        }
        var quotePrint = await browser.GetStringAsync($"/Cotizacions/Imprimir/{newId}");
        Assert.Contains("C001-004533", quotePrint);
        var details = await browser.GetStringAsync($"/Cotizacions/Details/{newId}");
        var confirmed = await browser.PostAsync("/Ventas/ConfirmarDesdeCotizacion", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cotizacionId"] = newId.ToString(), ["idAlmacen"] = warehouseId.ToString(), ["__RequestVerificationToken"] = Token(details)
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        int saleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Venta.SingleAsync();
            saleId = sale.IdVenta;
            Assert.Equal("V001", sale.SerieInterna);
            Assert.Equal(4532, sale.CorrelativoInterno);
            Assert.Equal("Cliente inicial", sale.ClienteNombreEmision);
            Assert.Equal(20m, sale.SubtotalSinIgvEmision);
            Assert.Equal(3.60m, sale.IgvEmision);
            var customer = await db.Cliente.SingleAsync();
            customer.Nombre = "Nombre actualizado";
            var product = await db.Producto.SingleAsync();
            product.Nombre = "Artículo renombrado";
            await db.SaveChangesAsync();
        }
        var quotePrintAfterChanges = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Cotizacions/Imprimir/{newId}"));
        Assert.Contains("Cliente inicial", quotePrintAfterChanges);
        Assert.Contains("Artículo", quotePrintAfterChanges);
        Assert.DoesNotContain("Artículo renombrado", quotePrintAfterChanges);
        var salePrint = await browser.GetStringAsync($"/Ventas/Imprimir/{saleId}");
        Assert.Contains("Cliente inicial", salePrint);
        Assert.DoesNotContain("Nombre actualizado", salePrint);
        Assert.Contains("NO VÁLIDO COMO COMPROBANTE DE PAGO TRIBUTARIO", salePrint);
        var saleDetails = await browser.GetStringAsync($"/Ventas/Details/{saleId}");
        var cancel = await browser.PostAsync($"/Ventas/Cancelar/{saleId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = saleId.ToString(), ["__RequestVerificationToken"] = Token(saleDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Contains("VENTA CANCELADA", await browser.GetStringAsync($"/Ventas/Imprimir/{saleId}"));
    }

    [Fact]
    public async Task DocumentAndUbigeoRulesValidateCustomersAndSuppliersWithoutLosingLegacyRecords()
    {
        using var factory = CreateFactory();
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "catalog-admin@example.test", Email = "catalog-admin@example.test" };
            Assert.True((await manager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DocumentoIdentidadTipos.AddRange(
                new DocumentoIdentidadTipo { IdDocumentoIdentidadTipo = 2, Codigo = "1", Nombre = "DNI", Abreviatura = "DNI", Estado = true, FechaCrea = DateTime.UtcNow },
                new DocumentoIdentidadTipo { IdDocumentoIdentidadTipo = 4, Codigo = "6", Nombre = "RUC", Abreviatura = "RUC", Estado = true, FechaCrea = DateTime.UtcNow });
            db.Ubigeos.AddRange(
                new Ubigeo { IdUbigeo = 1, Codigo = "150000", Descripcion = "Lima", Parent = "000000", IdParent = 0 },
                new Ubigeo { IdUbigeo = 2, Codigo = "150100", Descripcion = "Lima", Parent = "150000", IdParent = 1 },
                new Ubigeo { IdUbigeo = 3, Codigo = "150101", Descripcion = "Lima", Parent = "150100", IdParent = 2 });
            db.Cliente.Add(new Cliente { Nombre = "Cliente antiguo", Dni = "87654321" });
            db.Proveedor.Add(new Proveedor { Nombre = "Proveedor antiguo" });
            await db.SaveChangesAsync();
        }
        await Login(browser, "catalog-admin@example.test");
        var clientForm = await browser.GetStringAsync("/Clientes/Create");
        Assert.Contains("Tipo de documento", clientForm);
        var locations = await browser.GetAsync("/Ubigeos/Hijos?parent=150100");
        Assert.Contains("150101", await locations.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync("/Ubigeos/Hijos?parent=bogus")).StatusCode);

        var badCompany = await browser.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Empresa SAC", ["IdDocumentoIdentidadTipo"] = "4", ["NumeroDocumento"] = "20123456789",
            ["UbigeoCodigo"] = "150100", ["__RequestVerificationToken"] = Token(clientForm)
        }));
        Assert.Equal(HttpStatusCode.OK, badCompany.StatusCode);
        var errors = WebUtility.HtmlDecode(await badCompany.Content.ReadAsStringAsync());
        Assert.Contains("La dirección es obligatoria", errors);
        Assert.Contains("Selecciona un distrito válido", errors);

        var validCompany = await browser.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Empresa SAC", ["IdDocumentoIdentidadTipo"] = "4", ["NumeroDocumento"] = "20123456789",
            ["Direccion"] = "Av. Principal 123", ["UbigeoCodigo"] = "150101", ["__RequestVerificationToken"] = Token(clientForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, validCompany.StatusCode);
        var duplicate = await browser.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Duplicado", ["IdDocumentoIdentidadTipo"] = "4", ["NumeroDocumento"] = "20123456789",
            ["Direccion"] = "Av. Principal 123", ["UbigeoCodigo"] = "150101", ["__RequestVerificationToken"] = Token(clientForm)
        }));
        Assert.Contains("Ya existe un cliente", WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync()));

        var person = await browser.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Persona sin dirección", ["IdDocumentoIdentidadTipo"] = "2", ["NumeroDocumento"] = "12345678",
            ["__RequestVerificationToken"] = Token(clientForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, person.StatusCode);

        var supplierForm = await browser.GetStringAsync("/Proveedors/Create");
        var badSupplier = await browser.PostAsync("/Proveedors/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Nuevo proveedor", ["__RequestVerificationToken"] = Token(supplierForm)
        }));
        Assert.Equal(HttpStatusCode.OK, badSupplier.StatusCode);
        var badSupplierHtml = WebUtility.HtmlDecode(await badSupplier.Content.ReadAsStringAsync());
        Assert.Contains("RUC de 11 dígitos", badSupplierHtml);
        Assert.Contains("dirección del proveedor", badSupplierHtml);
        Assert.Contains("Selecciona departamento", badSupplierHtml);

        var supplier = await browser.PostAsync("/Proveedors/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Nuevo proveedor", ["Ruc"] = "20987654321", ["Direccion"] = "Calle 42",
            ["UbigeoCodigo"] = "150101", ["__RequestVerificationToken"] = Token(supplierForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, supplier.StatusCode);
        var repeatedSupplier = await browser.PostAsync("/Proveedors/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Otro proveedor", ["Ruc"] = "20987654321", ["Direccion"] = "Calle 42",
            ["UbigeoCodigo"] = "150101", ["__RequestVerificationToken"] = Token(supplierForm)
        }));
        Assert.Contains("Ya existe un proveedor", WebUtility.HtmlDecode(await repeatedSupplier.Content.ReadAsStringAsync()));
        int companyId;
        int supplierId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            companyId = (await db.Cliente.SingleAsync(item => item.Nombre == "Empresa SAC")).IdCliente;
            supplierId = (await db.Proveedor.SingleAsync(item => item.Nombre == "Nuevo proveedor")).IdProveedor;
        }
        var companyEditForm = await browser.GetStringAsync($"/Clientes/Edit/{companyId}");
        Assert.Contains("150101", companyEditForm);
        var companyEdit = await browser.PostAsync($"/Clientes/Edit/{companyId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdCliente"] = companyId.ToString(), ["Nombre"] = "Empresa SAC editada", ["IdDocumentoIdentidadTipo"] = "4",
            ["NumeroDocumento"] = "20123456789", ["Direccion"] = "Nueva sede", ["UbigeoCodigo"] = "150101",
            ["Dni"] = "00000000", ["__RequestVerificationToken"] = Token(companyEditForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, companyEdit.StatusCode);
        var supplierEditForm = await browser.GetStringAsync($"/Proveedors/Edit/{supplierId}");
        var supplierEdit = await browser.PostAsync($"/Proveedors/Edit/{supplierId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdProveedor"] = supplierId.ToString(), ["Nombre"] = "Proveedor corregido", ["Ruc"] = "20987654321",
            ["Direccion"] = "Calle nueva", ["UbigeoCodigo"] = "150101", ["__RequestVerificationToken"] = Token(supplierEditForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, supplierEdit.StatusCode);
        using var verify = factory.Services.CreateScope();
        var data = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await data.Cliente.CountAsync());
        Assert.Equal("87654321", (await data.Cliente.SingleAsync(item => item.Nombre == "Cliente antiguo")).Dni);
        var updatedCompany = await data.Cliente.SingleAsync(item => item.IdCliente == companyId);
        Assert.Equal("Empresa SAC editada", updatedCompany.Nombre);
        Assert.Equal("150101", updatedCompany.UbigeoCodigo);
        Assert.Null(updatedCompany.Dni); // Ignora el valor antiguo falsificado en la petición.
        Assert.Null((await data.Cliente.SingleAsync(item => item.Nombre == "Persona sin dirección")).Direccion);
        Assert.Equal(2, await data.Proveedor.CountAsync());
        Assert.Equal("Calle nueva", (await data.Proveedor.FindAsync(supplierId))!.Direccion);
    }

    [Fact]
    public async Task AdminCanAddAndRemoveIndividualRolesWithoutReplacingAllAssignments()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string targetUserId;
        string adminUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var admin = new IdentityUser { UserName = "roles-admin@example.test", Email = "roles-admin@example.test" };
            Assert.True((await manager.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(admin, "Admin")).Succeeded);
            adminUserId = admin.Id;

            foreach (var role in new[] { "Designer", "Staff", "Auditor" })
                Assert.True((await roleManager.CreateAsync(new IdentityRole(role))).Succeeded);

            var target = new IdentityUser { UserName = "target@example.test", Email = "target@example.test" };
            Assert.True((await manager.CreateAsync(target, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRolesAsync(target, new[] { "Designer", "Staff" })).Succeeded);
            targetUserId = target.Id;
        }

        await Login(client, "roles-admin@example.test");
        var assignmentHtml = await client.GetStringAsync($"/Admin/AssignRole?userId={targetUserId}");
        var response = await client.PostAsync("/Admin/AssignRole", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("UserId", targetUserId),
            new KeyValuePair<string, string>("SelectedRoles", "Designer"),
            new KeyValuePair<string, string>("SelectedRoles", "Auditor"),
            new KeyValuePair<string, string>("__RequestVerificationToken", Token(assignmentHtml))
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var userManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var updatedUser = await userManager.FindByIdAsync(targetUserId);
        Assert.NotNull(updatedUser);
        var roles = await userManager.GetRolesAsync(updatedUser!);
        Assert.Equal(new[] { "Auditor", "Designer" }, roles.OrderBy(role => role).ToArray());

        var verifyRoleManager = verifyScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var designerRole = await verifyRoleManager.FindByNameAsync("Designer");
        Assert.NotNull(designerRole);
        var editHtml = await client.GetStringAsync($"/Roles/Edit/{designerRole!.Id}");
        var editResponse = await client.PostAsync($"/Roles/Edit/{designerRole.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = designerRole.Id,
            ["Nombre"] = "Graphic Designer",
            ["__RequestVerificationToken"] = Token(editHtml)
        }));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        var renamedRoles = await userManager.GetRolesAsync(updatedUser!);
        Assert.Contains("Graphic Designer", renamedRoles);
        Assert.DoesNotContain("Designer", renamedRoles);

        var selfAssignmentHtml = await client.GetStringAsync($"/Admin/AssignRole?userId={adminUserId}");
        var rejectedDemotion = await client.PostAsync("/Admin/AssignRole", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserId"] = adminUserId,
            ["__RequestVerificationToken"] = Token(selfAssignmentHtml)
        }));
        Assert.Equal(HttpStatusCode.OK, rejectedDemotion.StatusCode);
        var rejectedDemotionResponseHtml = await rejectedDemotion.Content.ReadAsStringAsync();
        var rejectedDemotionHtml = WebUtility.HtmlDecode(rejectedDemotionResponseHtml);
        Assert.Contains("No puedes quitarte el rol Admin", rejectedDemotionHtml);
        Assert.Contains("se mantuvo la asignación", rejectedDemotionHtml);
        Assert.Contains("checked=\"checked\"", rejectedDemotionHtml);
        var actingAdmin = await userManager.FindByIdAsync(adminUserId);
        Assert.NotNull(actingAdmin);
        Assert.Contains("Admin", await userManager.GetRolesAsync(actingAdmin!));
    }

    [Fact]
    public async Task MutatingRequestWithoutTokenIsRejected()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "owner@example.test", Email = "owner@example.test" };
            Assert.True((await manager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
        }
        await Login(client, "owner@example.test");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Nombre"] = "Sin token"
            }))).StatusCode);
    }

    [Fact]
    public async Task SellerConfirmationCreatesSaleAndWorkAtomicallyAndCancellationRestoresItemStock()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "seller@example.test";
        int quoteId;
        int itemId;
        int serviceId;
        int warehouseId;

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roleManager.CreateAsync(new IdentityRole("Vendedor"))).Succeeded);
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            Assert.True((await userManager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, "Vendedor")).Succeeded);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menus = await AddMenusAsync(context);
            AddPermission(context, user, "clientes", menus, view: true, create: true, edit: true);
            AddPermission(context, user, "productos", menus, view: true);
            AddPermission(context, user, "cotizaciones", menus, view: true, create: true, edit: true);
            AddPermission(context, user, "ventas", menus, view: true, process: true);
            AddPermission(context, user, "taller", menus, view: true);

            var customer = new Cliente { Nombre = "Cliente de venta" };
            var item = new Producto { Nombre = "Papel", Precio = 10m, Stock = 5, EsServicio = false };
            var service = new Producto { Nombre = "Diseño", Precio = 20m, Stock = 0, EsServicio = true };
            var warehouse = new Almacen { Nombre = InventarioService.NombreAlmacen, EsPredeterminado = true, Activo = true, FechaActivacionUtc = DateTimeOffset.UtcNow };
            context.AddRange(customer, item, service, warehouse);
            await context.SaveChangesAsync();
            itemId = item.IdProducto;
            serviceId = service.IdProducto;
            warehouseId = warehouse.IdAlmacen;
            context.SaldosAlmacen.Add(new StockAlmacen { IdAlmacen = warehouseId, IdProducto = itemId, CantidadBase = 5m });
            await context.SaveChangesAsync();

            var quote = new Cotizacion
            {
                IdCliente = customer.IdCliente,
                IdUsuario = user.Id,
                FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow),
                FechaVencimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                Estado = "Emitida",
                Total = 47.20m
            };
            quote.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = itemId, Cantidad = 2, PrecioUnitario = 10m });
            quote.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = serviceId, Cantidad = 1, PrecioUnitario = 20m });
            context.Cotizacion.Add(quote);
            await context.SaveChangesAsync();
            quoteId = quote.IdCotizacion;
            await context.SaveChangesAsync();
        }

        await Login(client, email);
        var quoteDetails = await client.GetStringAsync($"/Cotizacions/Details/{quoteId}");
        Assert.Contains("Confirmar venta y crear trabajo", quoteDetails);
        var confirmation = await client.PostAsync("/Ventas/ConfirmarDesdeCotizacion", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cotizacionId"] = quoteId.ToString(),
            ["idAlmacen"] = warehouseId.ToString(),
            ["__RequestVerificationToken"] = Token(quoteDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);

        int saleId;
        int workId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await context.Venta.Include(item => item.Pedido).SingleAsync();
            saleId = sale.IdVenta;
            workId = sale.Pedido!.IdPedido;
            Assert.Equal("Confirmada", sale.Estado);
            Assert.Equal("En cola", sale.Pedido.EstadoPedido);
            Assert.Equal("Convertida", (await context.Cotizacion.FindAsync(quoteId))!.Estado);
            Assert.Equal(5, (await context.Producto.FindAsync(itemId))!.Stock);
            Assert.Equal(3m, (await context.SaldosAlmacen.FindAsync(warehouseId, itemId))!.CantidadBase);
            Assert.Equal(0, (await context.Producto.FindAsync(serviceId))!.Stock);
            Assert.Equal(3, await context.AuditoriaOperacion.CountAsync(entry => entry.TipoEntidad == "Pedido" || entry.TipoEntidad == "Venta" || entry.TipoEntidad == "Cotizacion"));
            var quoteTransition = await context.AuditoriaOperacion.SingleAsync(entry => entry.TipoEntidad == "Cotizacion"
                && entry.IdEntidad == quoteId && entry.Accion == "Convertida en venta");
            Assert.Equal("Emitida", quoteTransition.EstadoAnterior);
            Assert.Equal("Convertida", quoteTransition.EstadoNuevo);
            Assert.Equal((await context.Users.SingleAsync()).Id, quoteTransition.IdUsuario);
            Assert.Equal(TimeSpan.Zero, quoteTransition.FechaUtc.Offset);
            var saleTransition = await context.AuditoriaOperacion.SingleAsync(entry => entry.TipoEntidad == "Venta"
                && entry.Accion == "Venta confirmada");
            Assert.Null(saleTransition.EstadoAnterior);
            Assert.Equal(EstadoVenta.Confirmada, saleTransition.EstadoNuevo);
            var workTransition = await context.AuditoriaOperacion.SingleAsync(entry => entry.TipoEntidad == "Pedido"
                && entry.Accion == "Trabajo creado");
            Assert.Null(workTransition.EstadoAnterior);
            Assert.Equal(EstadoPedido.EnCola, workTransition.EstadoNuevo);
        }

        var editAfterSale = await client.GetAsync($"/Cotizacions/Edit/{quoteId}");
        Assert.Equal(HttpStatusCode.Redirect, editAfterSale.StatusCode);

        var saleDetails = await client.GetStringAsync($"/Ventas/Details/{saleId}");
        var cancellation = await client.PostAsync("/Ventas/Cancelar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = saleId.ToString(),
            ["__RequestVerificationToken"] = Token(saleDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, cancellation.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Cancelada", (await context.Venta.FindAsync(saleId))!.Estado);
            Assert.Equal("Cancelado", (await context.Pedido.FindAsync(workId))!.EstadoPedido);
            Assert.Equal(5, (await context.Producto.FindAsync(itemId))!.Stock);
            Assert.Equal(5m, (await context.SaldosAlmacen.FindAsync(warehouseId, itemId))!.CantidadBase);
            Assert.Equal(0, (await context.Producto.FindAsync(serviceId))!.Stock);
            var saleCancelled = await context.AuditoriaOperacion.SingleAsync(entry => entry.TipoEntidad == "Venta"
                && entry.IdEntidad == saleId && entry.Accion == "Venta cancelada");
            Assert.Equal(EstadoVenta.Confirmada, saleCancelled.EstadoAnterior);
            Assert.Equal(EstadoVenta.Cancelada, saleCancelled.EstadoNuevo);
            var workCancelled = await context.AuditoriaOperacion.SingleAsync(entry => entry.TipoEntidad == "Pedido"
                && entry.IdEntidad == workId && entry.Accion == "Trabajo cancelado");
            Assert.Equal(EstadoPedido.EnCola, workCancelled.EstadoAnterior);
            Assert.Equal(EstadoPedido.Cancelado, workCancelled.EstadoNuevo);

            var item = await context.Producto.FindAsync(itemId);
            var balance = await context.SaldosAlmacen.FindAsync(warehouseId, itemId);
            balance!.CantidadBase = 1m;
            var customer = await context.Cliente.SingleAsync();
            var seller = await context.Users.SingleAsync(user => user.Email == email);
            var insufficientQuote = new Cotizacion
            {
                IdCliente = customer.IdCliente,
                IdUsuario = seller.Id,
                FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow),
                FechaVencimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                Estado = "Emitida",
                Total = 23.60m
            };
            insufficientQuote.DetalleCotizacions.Add(new DetalleCotizacion { IdProducto = itemId, Cantidad = 2, PrecioUnitario = 10m });
            context.Cotizacion.Add(insufficientQuote);
            await context.SaveChangesAsync();
            quoteId = insufficientQuote.IdCotizacion;
        }

        var insufficientDetails = await client.GetStringAsync($"/Cotizacions/Details/{quoteId}");
        var rejectedConfirmation = await client.PostAsync("/Ventas/ConfirmarDesdeCotizacion", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cotizacionId"] = quoteId.ToString(),
            ["idAlmacen"] = warehouseId.ToString(),
            ["__RequestVerificationToken"] = Token(insufficientDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, rejectedConfirmation.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Emitida", (await context.Cotizacion.FindAsync(quoteId))!.Estado);
            Assert.Equal(5, (await context.Producto.FindAsync(itemId))!.Stock);
            Assert.Equal(1m, (await context.SaldosAlmacen.FindAsync(warehouseId, itemId))!.CantidadBase);
            Assert.Single(await context.Venta.ToListAsync());
            Assert.Single(await context.Pedido.ToListAsync());

            var customer = await context.Cliente.SingleAsync();
            var seller = await context.Users.SingleAsync(user => user.Email == email);
            context.Cotizacion.Add(new Cotizacion
            {
                IdCliente = customer.IdCliente,
                IdUsuario = seller.Id,
                FechaCreacion = FechaComercial.HoyPeru().AddDays(-10),
                FechaVencimiento = FechaComercial.HoyPeru().AddDays(-1),
                Estado = "Emitida",
                Total = 0m
            });
            await context.SaveChangesAsync();
        }

        var quotesIndex = await client.GetStringAsync("/Cotizacions");
        Assert.Contains("Vencida", quotesIndex);
        using var expirationScope = factory.Services.CreateScope();
        var expirationContext = expirationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expiredQuote = await expirationContext.Cotizacion.OrderBy(item => item.IdCotizacion).LastAsync();
        Assert.Equal("Vencida", expiredQuote.Estado);
        var expiryAudit = await expirationContext.AuditoriaOperacion.SingleAsync(entry => entry.Accion == "Vencimiento automático");
        Assert.Equal(expiredQuote.IdCotizacion, expiryAudit.IdEntidad);
        Assert.Null(expiryAudit.IdUsuario);
        Assert.Equal("Emitida", expiryAudit.EstadoAnterior);
        Assert.Equal("Vencida", expiryAudit.EstadoNuevo);
        Assert.Equal(TimeSpan.Zero, expiryAudit.FechaUtc.Offset);
        Assert.Equal(0, await new CotizacionExpirationService(expirationContext).ExpireDueAsync());
        Assert.Single(await expirationContext.AuditoriaOperacion.Where(entry => entry.Accion == "Vencimiento automático").ToListAsync());
    }

    [Fact]
    public async Task WorkshopRoleCanOnlyAdvanceWorkOneStepAndCannotOpenSales()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "operator@example.test";
        int workId;

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roleManager.CreateAsync(new IdentityRole("Operador de Taller"))).Succeeded);
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            Assert.True((await manager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Operador de Taller")).Succeeded);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menus = await AddMenusAsync(context);
            AddPermission(context, user, "taller", menus, view: true, process: true);
            var customer = new Cliente { Nombre = "Cliente taller" };
            var work = new Pedido { IdClienteNavigation = customer, EstadoPedido = "En cola", FechaPedido = DateOnly.FromDateTime(DateTime.UtcNow) };
            context.Add(work);
            await context.SaveChangesAsync();
            workId = work.IdPedido;
        }

        await Login(client, email);
        var deniedSales = await client.GetAsync("/Ventas");
        Assert.Equal(HttpStatusCode.Found, deniedSales.StatusCode);
        Assert.Contains("AccessDenied", deniedSales.Headers.Location?.ToString());
        var deniedAudit = await client.GetAsync("/Auditoria");
        Assert.Equal(HttpStatusCode.Found, deniedAudit.StatusCode);
        Assert.Contains("AccessDenied", deniedAudit.Headers.Location?.ToString());
        var deniedAnalytics = await client.GetAsync("/Analytics");
        Assert.Equal(HttpStatusCode.Found, deniedAnalytics.StatusCode);
        Assert.Contains("AccessDenied", deniedAnalytics.Headers.Location?.ToString());
        var deniedExcel = await client.GetAsync("/Analytics/ExportarExcel");
        Assert.Equal(HttpStatusCode.Found, deniedExcel.StatusCode);
        Assert.Contains("AccessDenied", deniedExcel.Headers.Location?.ToString());
        var deniedPdf = await client.GetAsync("/Analytics/ExportarPdf");
        Assert.Equal(HttpStatusCode.Found, deniedPdf.StatusCode);
        Assert.Contains("AccessDenied", deniedPdf.Headers.Location?.ToString());
        var details = await client.GetStringAsync($"/Taller/Details/{workId}");
        Assert.Contains("Avanzar a", details);
        var skipped = await client.PostAsync("/Taller/ActualizarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = workId.ToString(),
            ["estado"] = "Listo para recojo",
            ["__RequestVerificationToken"] = Token(details)
        }));
        Assert.Equal(HttpStatusCode.Redirect, skipped.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("En cola", (await context.Pedido.FindAsync(workId))!.EstadoPedido);
        }

        var advanced = await client.PostAsync("/Taller/ActualizarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = workId.ToString(),
            ["estado"] = "En impresión / acabado",
            ["__RequestVerificationToken"] = Token(details)
        }));
        Assert.Equal(HttpStatusCode.Redirect, advanced.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("En impresión / acabado", (await verifyContext.Pedido.FindAsync(workId))!.EstadoPedido);
        var transition = await verifyContext.AuditoriaOperacion.SingleAsync(entry => entry.TipoEntidad == "Pedido");
        Assert.Equal("En cola", transition.EstadoAnterior);
        Assert.Equal("En impresión / acabado", transition.EstadoNuevo);
        Assert.Equal(TimeSpan.Zero, transition.FechaUtc.Offset);

        foreach (var next in new[] { EstadoPedido.ListoParaRecojo, EstadoPedido.Entregado })
        {
            var currentDetails = await client.GetStringAsync($"/Taller/Details/{workId}");
            var moved = await client.PostAsync("/Taller/ActualizarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = workId.ToString(), ["estado"] = next,
                ["__RequestVerificationToken"] = Token(currentDetails)
            }));
            Assert.Equal(HttpStatusCode.Redirect, moved.StatusCode);
        }
        var deliveredDetails = await client.GetStringAsync($"/Taller/Details/{workId}");
        var cannotMoveBack = await client.PostAsync("/Taller/ActualizarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = workId.ToString(), ["estado"] = EstadoPedido.EnCola,
            ["__RequestVerificationToken"] = Token(deliveredDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, cannotMoveBack.StatusCode);
        using var verifyFinalScope = factory.Services.CreateScope();
        var finalContext = verifyFinalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(EstadoPedido.Entregado, (await finalContext.Pedido.FindAsync(workId))!.EstadoPedido);
        Assert.Equal(3, await finalContext.AuditoriaOperacion.CountAsync(entry => entry.TipoEntidad == "Pedido"));
    }

    [Fact]
    public async Task AssistedQuoteRequiresCreatePermissionReviewsOutboundTextAndNeverSavesWithoutSeller()
    {
        var fake = new CapturaSolicitudExtractor();
        using var factory = CreateFactory(fake);
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole("Vendedor"))).Succeeded);
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var seller = new IdentityUser { UserName = "ai-seller@example.test", Email = "ai-seller@example.test" };
            Assert.True((await manager.CreateAsync(seller, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(seller, "Vendedor")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menus = await AddMenusAsync(db);
            AddPermission(db, seller, "cotizaciones", menus, view: true);
            db.Add(new Cliente { Nombre = "Cliente asistido" });
            db.Add(new Producto { Nombre = "Papel", Precio = 10m, Stock = 100, CostoUnitarioSinIgv = 4m });
            await db.SaveChangesAsync();
        }
        await Login(browser, "ai-seller@example.test");
        Assert.Contains("AccessDenied", (await browser.GetAsync("/PreCotizacion")).Headers.Location?.ToString());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.PermisoRolMenu.SingleAsync(item => item.Menu!.Codigo == "cotizaciones")).PuedeCrear = true;
            await db.SaveChangesAsync();
        }

        var start = await browser.GetStringAsync("/PreCotizacion");
        var prepared = await browser.PostAsync("/PreCotizacion/Preparar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Solicitud"] = "Necesito 2 Papel couché para el viernes; contacto: Ana Pérez; correo ana@example.test, teléfono 999888777",
            ["__RequestVerificationToken"] = Token(start)
        }));
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);
        var preview = await prepared.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ana@example.test", preview, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("999888777", preview);
        Assert.Contains("Ana Pérez", WebUtility.HtmlDecode(preview));
        Assert.Contains("se extrajo localmente", WebUtility.HtmlDecode(preview));
        Assert.Contains("[CORREO]", preview);
        Assert.Empty(fake.Textos);
        var match = Regex.Match(preview, "name=\"TextoRevisado\"[^>]*>(.*?)</textarea>", RegexOptions.Singleline);
        Assert.True(match.Success);
        var sanitized = WebUtility.HtmlDecode(match.Groups[1].Value);
        Assert.DoesNotContain("Ana Pérez", sanitized);

        var rechecked = await browser.PostAsync("/PreCotizacion/Analizar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TextoRevisado"] = "Papel couché a color. Escríbeme a ana@example.test",
            ["TextoAprobado"] = "true",
            ["__RequestVerificationToken"] = Token(preview)
        }));
        Assert.Equal(HttpStatusCode.OK, rechecked.StatusCode);
        var recheckedHtml = await rechecked.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ana@example.test", recheckedHtml);
        Assert.Contains("Revisa de nuevo", recheckedHtml);
        Assert.Empty(fake.Textos);

        var rejected = await browser.PostAsync("/PreCotizacion/Analizar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TextoRevisado"] = sanitized,
            ["__RequestVerificationToken"] = Token(preview)
        }));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Empty(fake.Textos);

        var reviewed = await rejected.Content.ReadAsStringAsync();
        var result = await browser.PostAsync("/PreCotizacion/Analizar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TextoRevisado"] = sanitized,
            ["TextoAprobado"] = "true",
            ["__RequestVerificationToken"] = Token(reviewed)
        }));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(sanitized.Trim(), Assert.Single(fake.Textos));
        var suggestions = await result.Content.ReadAsStringAsync();
        Assert.Contains("Cliente registrado", suggestions);
        Assert.Contains("Papel", suggestions);
        int clientId;
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            clientId = (await db.Cliente.SingleAsync()).IdCliente;
            productId = (await db.Producto.SingleAsync()).IdProducto;
            Assert.Empty(await db.Cotizacion.ToListAsync());
        }

        var loaded = await browser.PostAsync("/PreCotizacion/Cargar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = clientId.ToString(),
            ["Lineas[0].NombreSugerido"] = "Papel",
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].Cantidad"] = "2",
            ["Descripcion"] = "Papel couché para viernes",
            ["__RequestVerificationToken"] = Token(suggestions)
        }));
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        var quoteForm = await loaded.Content.ReadAsStringAsync();
        Assert.Contains("action=\"/Cotizacions/Create\"", quoteForm);
        Assert.Contains("20.00", quoteForm);
        Assert.Contains("Referencia de precio", quoteForm);
        Assert.Contains("4.45", quoteForm);
        Assert.DoesNotContain("Última venta confirmada", quoteForm);
        using (var scope = factory.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cotizacion.ToListAsync());

        var belowMargin = await browser.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = clientId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].Cantidad"] = "2",
            ["Lineas[0].PrecioUnitario"] = "4",
            ["__RequestVerificationToken"] = Token(quoteForm)
        }));
        Assert.Equal(HttpStatusCode.OK, belowMargin.StatusCode);
        Assert.Contains("margen mínimo del 10 %", WebUtility.HtmlDecode(await belowMargin.Content.ReadAsStringAsync()));
        using (var scope = factory.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cotizacion.ToListAsync());

        var saved = await browser.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = clientId.ToString(),
            ["Lineas[0].ProductoId"] = productId.ToString(),
            ["Lineas[0].Cantidad"] = "2",
            ["Lineas[0].PrecioUnitario"] = "10",
            ["Descripcion"] = "Papel couché para viernes",
            ["__RequestVerificationToken"] = Token(quoteForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using var verification = factory.Services.CreateScope();
        Assert.Single(await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cotizacion.ToListAsync());
    }

    [Fact]
    public async Task LocalIdentityCandidateCanPrefillAnExactCustomerMatchWithoutSendingDniToGroq()
    {
        var extractor = new CapturaSolicitudExtractor();
        using var factory = CreateFactory(extractor);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "identity-candidate-admin@example.test";
        int customerId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DocumentoIdentidadTipos.Add(new DocumentoIdentidadTipo
            {
                IdDocumentoIdentidadTipo = 2, Codigo = "1", Nombre = "DNI", Abreviatura = "DNI", Estado = true
            });
            var customer = new Cliente
            {
                Nombre = "Cliente identificado", IdDocumentoIdentidadTipo = 2,
                NumeroDocumento = "12345678", Dni = "12345678"
            };
            db.AddRange(customer,
                new Producto { Nombre = "Papel couché", Descripcion = "Papel A4 de acabado brillante", Precio = 10m, Stock = 100 },
                new Producto { Nombre = "Papel bond", Descripcion = "Papel A4 mate", Precio = 8m, Stock = 100 });
            await db.SaveChangesAsync();
            customerId = customer.IdCliente;
        }

        await Login(client, email);
        var start = await client.GetStringAsync("/PreCotizacion");
        var preparedResponse = await client.PostAsync("/PreCotizacion/Preparar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Solicitud"] = "Cliente: Cliente identificado; DNI: 12345678; necesito 2 Papel A4",
            ["__RequestVerificationToken"] = Token(start)
        }));
        var prepared = WebUtility.HtmlDecode(await preparedResponse.Content.ReadAsStringAsync());
        Assert.Contains("Cliente identificado", prepared);
        Assert.Contains("DNI 12345678", prepared);
        var textMatch = Regex.Match(prepared, "name=\"TextoRevisado\"[^>]*>(.*?)</textarea>", RegexOptions.Singleline);
        Assert.True(textMatch.Success);
        var reviewedText = WebUtility.HtmlDecode(textMatch.Groups[1].Value);
        Assert.DoesNotContain("Cliente identificado", reviewedText);
        Assert.DoesNotContain("12345678", reviewedText);

        var analyzedResponse = await client.PostAsync("/PreCotizacion/Analizar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TextoRevisado"] = reviewedText,
            ["TextoAprobado"] = "true",
            ["ConfirmarIdentidadLocal"] = "true",
            ["ClienteNombreSugerido"] = "Cliente identificado",
            ["ClienteDocumentoTipoSugerido"] = "1",
            ["ClienteDocumentoNumeroSugerido"] = "12345678",
            ["__RequestVerificationToken"] = Token(prepared)
        }));
        Assert.Equal(HttpStatusCode.OK, analyzedResponse.StatusCode);
        var analyzed = WebUtility.HtmlDecode(await analyzedResponse.Content.ReadAsStringAsync());
        Assert.Contains("coincidencia exacta sugerida", analyzed);
        Assert.Contains($"value=\"{customerId}\" selected=\"selected\"", analyzed);
        Assert.Contains("Sugerencias para revisar", analyzed);
        Assert.Contains("Papel couché", analyzed);
        Assert.Contains("Papel bond", analyzed);
        Assert.DoesNotContain("Cliente identificado", extractor.Textos.Single());
        Assert.DoesNotContain("12345678", extractor.Textos.Single());
        var unselectedProduct = await client.PostAsync("/PreCotizacion/Cargar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClienteId"] = customerId.ToString(), ["Lineas[0].NombreSugerido"] = "Papel",
            ["Lineas[0].Cantidad"] = "2", ["Lineas[0].Especificacion"] = "A4",
            ["Descripcion"] = "Papel para cliente", ["__RequestVerificationToken"] = Token(analyzed)
        }));
        Assert.Equal(HttpStatusCode.OK, unselectedProduct.StatusCode);
        Assert.Contains("Selecciona un producto del catálogo", WebUtility.HtmlDecode(await unselectedProduct.Content.ReadAsStringAsync()));
        using var quoteCheck = factory.Services.CreateScope();
        Assert.Empty(await quoteCheck.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cotizacion.ToListAsync());
        using var verifyScope = factory.Services.CreateScope();
        Assert.Empty(await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Cotizacion.ToListAsync());
    }

    [Fact]
    public async Task AdminCreatesUsersAndPublicRegistrationIsClosed()
    {
        using var factory = CreateFactory();
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "user-admin@example.test", Email = "user-admin@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
        }

        var anonymousRegister = await adminClient.GetAsync("/Identity/Account/Register");
        Assert.Equal(HttpStatusCode.Redirect, anonymousRegister.StatusCode);
        Assert.Contains("/Identity/Account/Login", anonymousRegister.Headers.Location?.ToString());
        await Login(adminClient, "user-admin@example.test");

        var createForm = await adminClient.GetStringAsync("/Admin/CrearUsuario");
        var created = await adminClient.PostAsync("/Admin/CrearUsuario", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Correo"] = "new-staff@example.test", ["Password"] = "TestPassword123!",
            ["ConfirmarPassword"] = "TestPassword123!", ["__RequestVerificationToken"] = Token(createForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        string newUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var target = await users.FindByEmailAsync("new-staff@example.test");
            Assert.NotNull(target);
            newUserId = target!.Id;
            Assert.Empty(await users.GetRolesAsync(target));
        }

        using var staffClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(staffClient, "new-staff@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await staffClient.GetAsync("/Clientes")).StatusCode);

        var adminIndex = await adminClient.GetStringAsync("/Admin");
        var deactivated = await adminClient.PostAsync("/Admin/CambiarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userId"] = newUserId, ["__RequestVerificationToken"] = Token(adminIndex)
        }));
        Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await staffClient.GetAsync("/Identity/Usuario/Panel")).StatusCode);
        using var verify = factory.Services.CreateScope();
        var disabled = await verify.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByIdAsync(newUserId);
        Assert.NotNull(disabled);
        Assert.True(disabled!.LockoutEnd > DateTimeOffset.UtcNow.AddYears(50));
    }

    [Fact]
    public async Task SunatCatalogsAreReadableAndStateChangesAreAudited()
    {
        using var factory = CreateFactory();
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "sunat-admin@example.test", Email = "sunat-admin@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TiposDocumentoPagoSunat.Add(new TipoDocumentoPagoSunat
            {
                IdTipoDocumentoPago = 2, CodigoSunat = "01", Descripcion = "Factura", DescripcionCorta = "FA", Estado = true
            });
            await db.SaveChangesAsync();
        }
        await Login(adminClient, "sunat-admin@example.test");

        var page = await adminClient.GetStringAsync("/CatalogosSunat/TiposComprobante?q=Factura");
        Assert.Contains("Factura", page);
        Assert.Contains("01", page);
        var disabled = await adminClient.PostAsync("/CatalogosSunat/CambiarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["catalogo"] = "tipos-comprobante", ["id"] = "2", ["activo"] = "false", ["q"] = "Factura",
            ["pagina"] = "1", ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
        var resultHtml = WebUtility.HtmlDecode(await adminClient.GetStringAsync(disabled.Headers.Location!.ToString()));
        using var verify = factory.Services.CreateScope();
        var dbCheck = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await dbCheck.TiposDocumentoPagoSunat.SingleAsync()).Estado, resultHtml);
        Assert.Contains(await dbCheck.AuditoriaOperacion.ToListAsync(), item => item.Accion == "Registro desactivado");
    }

    [Fact]
    public async Task SunatCatalogStateChangeRequiresProcessPermissionForItsSubmenu()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "catalog-viewer@example.test";
        string roleId;
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole("Catalog Viewer"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Catalog Viewer")).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var root = new MenuSistema { Codigo = "catalogos-sunat", Nombre = "Catálogos SUNAT", Destino = "catalogos-sunat", ControllerNombre = "CatalogosSunat", Orden = 1 };
            db.MenuSistema.Add(root);
            await db.SaveChangesAsync();
            var leaf = new MenuSistema
            {
                Codigo = "sunat-tipos-comprobante", Nombre = "Tipos de comprobante", Destino = "sunat-tipos-comprobante",
                ControllerNombre = "CatalogosSunat", IdMenuPadre = root.IdMenu, Orden = 1
            };
            db.MenuSistema.Add(leaf);
            db.TiposDocumentoPagoSunat.Add(new TipoDocumentoPagoSunat
            {
                IdTipoDocumentoPago = 55, CodigoSunat = "99", Descripcion = "Prueba de permiso", Estado = true
            });
            roleId = (await roles.FindByNameAsync("Catalog Viewer"))!.Id;
            db.PermisoRolMenu.Add(new PermisoRolMenu { RoleId = roleId, MenuSistemaId = leaf.IdMenu, PuedeVer = true });
            await db.SaveChangesAsync();
        }

        await Login(client, email);
        var page = await client.GetStringAsync("/CatalogosSunat/TiposComprobante");
        Assert.Contains("Prueba de permiso", page);
        Assert.DoesNotContain("CambiarEstado", page);
        var denied = await client.PostAsync("/CatalogosSunat/CambiarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["catalogo"] = "tipos-comprobante", ["id"] = "55", ["activo"] = "false",
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.ToString());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var permission = await db.PermisoRolMenu.SingleAsync(item => item.RoleId == roleId);
            permission.PuedeProcesar = true;
            await db.SaveChangesAsync();
        }
        page = await client.GetStringAsync("/CatalogosSunat/TiposComprobante");
        var accepted = await client.PostAsync("/CatalogosSunat/CambiarEstado", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["catalogo"] = "tipos-comprobante", ["id"] = "55", ["activo"] = "false",
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        using var verification = factory.Services.CreateScope();
        Assert.False((await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().TiposDocumentoPagoSunat.SingleAsync()).Estado);
    }

    [Fact]
    public async Task ActiveRoleSwitchChangesAuthorizationAndSidebarWithoutCombiningGrants()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        string adminRoleId;
        string sellerRoleId;
        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roleManager.CreateAsync(new IdentityRole("Vendedor"))).Succeeded);
            adminRoleId = (await roleManager.FindByNameAsync("Admin"))!.Id;
            sellerRoleId = (await roleManager.FindByNameAsync("Vendedor"))!.Id;
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "multi-role@example.test", Email = "multi-role@example.test" };
            Assert.True((await users.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRolesAsync(user, new[] { "Admin", "Vendedor" })).Succeeded);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menus = await AddMenusAsync(db);
            db.PermisoRolMenu.Add(new PermisoRolMenu
            {
                RoleId = sellerRoleId, MenuSistemaId = menus["clientes"].IdMenu, PuedeVer = true
            });
            await db.SaveChangesAsync();
        }

        await Login(client, "multi-role@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
        var panel = await client.GetStringAsync("/Identity/Usuario/Panel");
        var switched = await client.PostAsync("/RoleSelection/Cambiar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["roleId"] = sellerRoleId, ["returnUrl"] = "/Identity/Usuario/Panel",
            ["__RequestVerificationToken"] = Token(panel)
        }));
        Assert.Equal(HttpStatusCode.Redirect, switched.StatusCode);
        var adminDenied = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, adminDenied.StatusCode);
        Assert.Contains("AccessDenied", adminDenied.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Clientes")).StatusCode);
        var productDenied = await client.GetAsync("/Productoes");
        Assert.Equal(HttpStatusCode.Redirect, productDenied.StatusCode);
        Assert.Contains("AccessDenied", productDenied.Headers.Location?.ToString());

        var clientPage = await client.GetStringAsync("/Clientes");
        var switchedBack = await client.PostAsync("/RoleSelection/Cambiar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["roleId"] = adminRoleId, ["returnUrl"] = "/Identity/Usuario/Panel",
            ["__RequestVerificationToken"] = Token(clientPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, switchedBack.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
    }

    [Fact]
    public async Task MenuManagerSupportsThreeLevelsAndRejectsCyclesAndUnregisteredDestinations()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = "menus-admin@example.test", Email = "menus-admin@example.test" };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
        }
        await Login(client, "menus-admin@example.test");

        var rootFormResponse = await client.GetAsync("/Menus/Create");
        var rootForm = await rootFormResponse.Content.ReadAsStringAsync();
        Assert.True(rootFormResponse.IsSuccessStatusCode, rootForm);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Menus/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Codigo"] = "comercial-test", ["Nombre"] = "Comercial", ["Orden"] = "10",
            ["__RequestVerificationToken"] = Token(rootForm)
        }))).StatusCode);
        int rootId;
        using (var scope = factory.Services.CreateScope())
            rootId = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MenuSistema.SingleAsync(menu => menu.Codigo == "comercial-test")).IdMenu;

        var childForm = await client.GetStringAsync("/Menus/Create");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Menus/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Codigo"] = "clientes-group", ["Nombre"] = "Clientes", ["IdMenuPadre"] = rootId.ToString(), ["Orden"] = "10",
            ["__RequestVerificationToken"] = Token(childForm)
        }))).StatusCode);
        int childId;
        using (var scope = factory.Services.CreateScope())
            childId = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MenuSistema.SingleAsync(menu => menu.Codigo == "clientes-group")).IdMenu;

        var leafForm = await client.GetStringAsync("/Menus/Create");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Menus/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Codigo"] = "clientes-page", ["Nombre"] = "Clientes", ["IdMenuPadre"] = childId.ToString(),
            ["Destino"] = "clientes", ["Orden"] = "10", ["__RequestVerificationToken"] = Token(leafForm)
        }))).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var leaf = await db.MenuSistema.SingleAsync(menu => menu.Codigo == "clientes-page");
            Assert.Equal(childId, leaf.IdMenuPadre);
            Assert.Equal("Clientes", leaf.ControllerNombre);
        }

        var depthForm = await client.GetStringAsync("/Menus/Create");
        var tooDeep = await client.PostAsync("/Menus/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Codigo"] = "menu-cuarto-nivel", ["Nombre"] = "Cuarto nivel", ["IdMenuPadre"] = (await GetMenuIdAsync(factory, "clientes-page")).ToString(),
            ["Orden"] = "5", ["__RequestVerificationToken"] = Token(depthForm)
        }));
        Assert.Equal(HttpStatusCode.OK, tooDeep.StatusCode);
        Assert.Contains("tres niveles", WebUtility.HtmlDecode(await tooDeep.Content.ReadAsStringAsync()));

        var cycleForm = await client.GetStringAsync($"/Menus/Edit/{rootId}");
        var cycle = await client.PostAsync("/Menus/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdMenu"] = rootId.ToString(), ["Codigo"] = "comercial-test", ["Nombre"] = "Comercial",
            ["IdMenuPadre"] = childId.ToString(), ["Orden"] = "10", ["__RequestVerificationToken"] = Token(cycleForm)
        }));
        Assert.Equal(HttpStatusCode.OK, cycle.StatusCode);
        Assert.Contains("descendientes", WebUtility.HtmlDecode(await cycle.Content.ReadAsStringAsync()));

        var invalidForm = await client.GetStringAsync("/Menus/Create");
        var unregistered = await client.PostAsync("/Menus/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Codigo"] = "enlace-ajeno", ["Nombre"] = "Enlace ajeno", ["Destino"] = "https://example.test",
            ["Orden"] = "20", ["__RequestVerificationToken"] = Token(invalidForm)
        }));
        Assert.Equal(HttpStatusCode.OK, unregistered.StatusCode);
        Assert.Contains("destino registrado", WebUtility.HtmlDecode(await unregistered.Content.ReadAsStringAsync()));

        var nav = await client.GetStringAsync("/Identity/Usuario/Panel");
        Assert.Contains("Comercial", nav);
        Assert.Contains("Clientes", nav);
    }

    [Fact]
    public async Task GroupMenuPermissionDoesNotGrantAccessToItsChildScreen()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "group-permission@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roleManager.CreateAsync(new IdentityRole("Vendedor"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Vendedor")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var group = new MenuSistema { Codigo = "comercial", Nombre = "Comercial", Orden = 1 };
            db.MenuSistema.Add(group);
            await db.SaveChangesAsync();
            var leaf = new MenuSistema
            {
                Codigo = "clientes", Nombre = "Clientes", ControllerNombre = "Clientes", Destino = "clientes",
                IdMenuPadre = group.IdMenu, Orden = 1
            };
            var quotes = new MenuSistema
            {
                Codigo = "cotizaciones", Nombre = "Cotizaciones", ControllerNombre = "Cotizacions", Destino = "cotizaciones",
                IdMenuPadre = group.IdMenu, Orden = 2
            };
            db.MenuSistema.AddRange(leaf, quotes);
            await db.SaveChangesAsync();
            var aiQuote = new MenuSistema
            {
                Codigo = "precotizacion", Nombre = "Pre-cotización con IA", ControllerNombre = "PreCotizacion",
                Destino = "precotizacion", IdMenuPadre = quotes.IdMenu, Orden = 1
            };
            db.MenuSistema.Add(aiQuote);
            await db.SaveChangesAsync();
            var roleId = (await roleManager.FindByNameAsync("Vendedor"))!.Id;
            db.PermisoRolMenu.AddRange(
                new PermisoRolMenu { RoleId = roleId, MenuSistemaId = group.IdMenu, PuedeVer = true },
                new PermisoRolMenu { RoleId = roleId, MenuSistemaId = quotes.IdMenu, PuedeVer = true, PuedeCrear = true });
            await db.SaveChangesAsync();
        }

        await Login(client, email);
        var denied = await client.GetAsync("/Clientes");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.ToString());
        var aiDenied = await client.GetAsync("/PreCotizacion");
        Assert.Equal(HttpStatusCode.Redirect, aiDenied.StatusCode);
        Assert.Contains("AccessDenied", aiDenied.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Cotizacions")).StatusCode);
        var panel = await client.GetStringAsync("/Identity/Usuario/Panel");
        Assert.DoesNotContain("href=\"/Clientes\"", panel);
        Assert.Contains("Cotizaciones", panel);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var group = await db.MenuSistema.SingleAsync(menu => menu.Codigo == "comercial");
            group.Activo = false;
            await db.SaveChangesAsync();
        }
        var inactiveParentDenied = await client.GetAsync("/Cotizacions");
        Assert.Equal(HttpStatusCode.Redirect, inactiveParentDenied.StatusCode);
        Assert.Contains("AccessDenied", inactiveParentDenied.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PasswordRecoveryTokenWorksOnceAndReplayIsRejected()
    {
        var sender = new CapturaIdentityEmailSender();
        using var factory = CreateFactory(emailSender: sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "reset-once@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.CreateAsync(new IdentityUser
            {
                UserName = email, Email = email, EmailConfirmed = true
            }, "OldPassword123!")).Succeeded);
        }

        var forgot = await client.GetStringAsync("/Identity/Account/ForgotPassword");
        var requested = await client.PostAsync("/Identity/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["__RequestVerificationToken"] = Token(forgot)
        }));
        Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
        Assert.Equal(email, sender.Recipient);
        var linkMatch = Regex.Match(sender.HtmlMessage!, "href='([^']+)'");
        Assert.True(linkMatch.Success);
        var resetUrl = new Uri(WebUtility.HtmlDecode(linkMatch.Groups[1].Value));
        var code = QueryHelpers.ParseQuery(resetUrl.Query)["code"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        async Task<HttpResponseMessage> Reset(string password)
        {
            var form = await client.GetStringAsync(resetUrl.PathAndQuery);
            var codeInput = Regex.Match(form, "name=\"Input.Code\"[^>]*value=\"([^\"]+)\"");
            Assert.True(codeInput.Success);
            return await client.PostAsync("/Identity/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.ConfirmPassword"] = password,
                ["Input.Code"] = WebUtility.HtmlDecode(codeInput.Groups[1].Value),
                ["__RequestVerificationToken"] = Token(form)
            }));
        }

        var firstUse = await Reset("NewPassword123!");
        Assert.Equal(HttpStatusCode.Redirect, firstUse.StatusCode);
        var replay = await Reset("AnotherPassword123!");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains("El enlace ya no es válido. Solicita uno nuevo.", WebUtility.HtmlDecode(await replay.Content.ReadAsStringAsync()));
        using var verifyScope = factory.Services.CreateScope();
        var verifyUsers = verifyScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var updated = await verifyUsers.FindByEmailAsync(email);
        Assert.True(await verifyUsers.CheckPasswordAsync(updated!, "NewPassword123!"));
        Assert.False(await verifyUsers.CheckPasswordAsync(updated!, "AnotherPassword123!"));
    }

    [Fact]
    public async Task ExpiredPasswordRecoveryTokenIsRejected()
    {
        var sender = new CapturaIdentityEmailSender();
        using var factory = CreateFactory(emailSender: sender, resetTokenLifespan: TimeSpan.FromSeconds(-1));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "reset-expired@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.CreateAsync(new IdentityUser
            {
                UserName = email, Email = email, EmailConfirmed = true
            }, "OldPassword123!")).Succeeded);
        }

        var forgot = await client.GetStringAsync("/Identity/Account/ForgotPassword");
        await client.PostAsync("/Identity/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email, ["__RequestVerificationToken"] = Token(forgot)
        }));
        var link = Regex.Match(sender.HtmlMessage!, "href='([^']+)'");
        Assert.True(link.Success);
        var resetUrl = new Uri(WebUtility.HtmlDecode(link.Groups[1].Value));
        var resetForm = await client.GetStringAsync(resetUrl.PathAndQuery);
        var codeInput = Regex.Match(resetForm, "name=\"Input.Code\"[^>]*value=\"([^\"]+)\"");
        Assert.True(codeInput.Success);
        var expired = await client.PostAsync("/Identity/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "NewPassword123!",
            ["Input.ConfirmPassword"] = "NewPassword123!",
            ["Input.Code"] = WebUtility.HtmlDecode(codeInput.Groups[1].Value),
            ["__RequestVerificationToken"] = Token(resetForm)
        }));
        Assert.Equal(HttpStatusCode.OK, expired.StatusCode);
        Assert.Contains("El enlace ya no es válido. Solicita uno nuevo.", WebUtility.HtmlDecode(await expired.Content.ReadAsStringAsync()));
    }

    [Fact]
    public void DataProtectionKeyRingSurvivesApplicationProviderRestart()
    {
        const string purpose = "ProyectoFinal.PersistenceTest.v1";
        byte[] protectedValue;
        using (var firstFactory = CreateFactory())
        using (var scope = firstFactory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
            protectedValue = provider.CreateProtector(purpose).Protect(System.Text.Encoding.UTF8.GetBytes("token-survives-restart"));
        }

        using var secondFactory = CreateFactory();
        using var secondScope = secondFactory.Services.CreateScope();
        var secondProvider = secondScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
        var clearValue = secondProvider.CreateProtector(purpose).Unprotect(protectedValue);
        Assert.Equal("token-survives-restart", System.Text.Encoding.UTF8.GetString(clearValue));
    }

    [Fact]
    public async Task LoginLocksOutAfterConfiguredFailedAttempts()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string email = "lockout@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True(user.LockoutEnabled);
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var form = await client.GetStringAsync("/Identity/Account/Login");
            await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "WrongPassword123!",
                ["__RequestVerificationToken"] = Token(form)
            }));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
            Assert.NotNull(user?.LockoutEnd);
            Assert.True(user!.LockoutEnd > DateTimeOffset.UtcNow);
        }

        var lockedForm = await client.GetStringAsync("/Identity/Account/Login");
        var locked = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "TestPassword123!",
            ["__RequestVerificationToken"] = Token(lockedForm)
        }));
        Assert.Contains("Lockout", locked.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AdminCanConfigureEncryptedSmtpParametersAndNonAdminCannotOpenThem()
    {
        using var factory = CreateFactory();
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string adminEmail = "smtp-admin@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(admin, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
        }

        await Login(adminClient, adminEmail);
        var form = await adminClient.GetStringAsync("/Parametros");
        Assert.Contains("Correo SMTP", form);
        var saved = await adminClient.PostAsync("/Parametros/Guardar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Servidor"] = "smtp.example.test",
            ["Puerto"] = "587",
            ["CorreoRemitente"] = "noreply@example.test",
            ["NombreRemitente"] = "Imprenta Collantes",
            ["Usuario"] = "smtp-user",
            ["Contrasena"] = "smtp-secret-test",
            ["UsarTls"] = "true",
            ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var settings = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ConfiguracionCorreo.SingleAsync();
            Assert.NotEqual("smtp-secret-test", settings.ContrasenaProtegida);
            Assert.Contains("smtp.example.test", settings.Servidor);
        }

        using var sellerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string sellerEmail = "smtp-seller@example.test";
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole("Vendedor"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var seller = new IdentityUser { UserName = sellerEmail, Email = sellerEmail, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(seller, "TestPassword123!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(seller, "Vendedor")).Succeeded);
        }
        await Login(sellerClient, sellerEmail);
        var denied = await sellerClient.GetAsync("/Parametros");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.ToString());
    }

    private sealed class CapturaSolicitudExtractor : ISolicitudExtractor
    {
        public List<string> Textos { get; } = new();

        public Task<ResultadoExtraccion> ExtraerAsync(string textoRevisado, CancellationToken cancellationToken)
        {
            Textos.Add(textoRevisado);
            return Task.FromResult(new ResultadoExtraccion(true, "Listo",
                new SolicitudExtraida(new[] { new LineaExtraida("Papel", 2, "Couché") }, "Impresión a color", "viernes")));
        }
    }

    private static async Task<Dictionary<string, MenuSistema>> AddMenusAsync(ApplicationDbContext context)
    {
        var menus = new[]
        {
            new MenuSistema { Codigo = "clientes", Nombre = "Clientes", ControllerNombre = "Clientes", Orden = 10 },
            new MenuSistema { Codigo = "productos", Nombre = "Productos", ControllerNombre = "Productoes", Orden = 20 },
            new MenuSistema { Codigo = "cotizaciones", Nombre = "Cotizaciones", ControllerNombre = "Cotizacions", Orden = 30 },
            new MenuSistema { Codigo = "ventas", Nombre = "Ventas", ControllerNombre = "Ventas", Orden = 40 },
            new MenuSistema { Codigo = "taller", Nombre = "Taller", ControllerNombre = "Taller", Orden = 50 }
        };
        context.MenuSistema.AddRange(menus);
        await context.SaveChangesAsync();
        return menus.ToDictionary(menu => menu.Codigo);
    }

    private sealed class CapturaCorreoComercialSender : ICorreoComercialSender
    {
        public string Recipient { get; private set; } = string.Empty;
        public string Subject { get; private set; } = string.Empty;
        public byte[]? Attachment { get; private set; }

        public Task SendEmailWithAttachmentAsync(string email, string subject, string htmlMessage,
            byte[] attachmentContent, string attachmentName, string contentType)
        {
            Recipient = email;
            Subject = subject;
            Attachment = attachmentContent;
            Assert.Equal("application/pdf", contentType);
            Assert.EndsWith(".pdf", attachmentName, StringComparison.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturaIdentityEmailSender : IEmailSender
    {
        public string Recipient { get; private set; } = string.Empty;
        public string Subject { get; private set; } = string.Empty;
        public string? HtmlMessage { get; private set; }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Recipient = email;
            Subject = subject;
            HtmlMessage = htmlMessage;
            return Task.CompletedTask;
        }
    }

    private static async Task<int> GetMenuIdAsync(WebApplicationFactory<Program> factory, string code)
    {
        using var scope = factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MenuSistema.SingleAsync(menu => menu.Codigo == code)).IdMenu;
    }

    private static void AddPermission(
        ApplicationDbContext context,
        IdentityUser user,
        string menuCode,
        IReadOnlyDictionary<string, MenuSistema> menus,
        bool view = false,
        bool create = false,
        bool edit = false,
        bool delete = false,
        bool process = false) => context.PermisoRolMenu.Add(new PermisoRolMenu
        {
            RoleId = context.UserRoles.Where(userRole => userRole.UserId == user.Id)
                .Select(userRole => userRole.RoleId).First(),
            MenuSistemaId = menus[menuCode].IdMenu,
            PuedeVer = view,
            PuedeCrear = create,
            PuedeEditar = edit,
            PuedeEliminar = delete,
            PuedeProcesar = process
        });

    private static async Task<HttpResponseMessage> Login(HttpClient client, string email)
    {
        var loginHtml = await client.GetStringAsync("/Identity/Account/Login");
        var response = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "TestPassword123!",
            ["__RequestVerificationToken"] = Token(loginHtml)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response;
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Form should contain an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

}
