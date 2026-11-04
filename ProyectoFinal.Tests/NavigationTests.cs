using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class NavigationTests
{
    private static WebApplicationFactory<Program> CreateFactory(ISolicitudExtractor? extractor = null)
    {
        var database = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=Unused;TrustServerCertificate=True",
                ["BootstrapAdmin:Email"] = "",
                ["BootstrapAdmin:Password"] = ""
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(database));
                if (extractor is not null)
                {
                    services.RemoveAll<ISolicitudExtractor>();
                    services.AddSingleton(extractor);
                }
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
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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
        }
        await Login(client, "admin@example.test");

        var panel = await client.GetAsync("/Identity/Usuario/Panel");
        Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
        Assert.Contains("Clientes", await panel.Content.ReadAsStringAsync());
        Assert.Contains("Series internas", await panel.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/SeriesInternas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Clientes")).StatusCode);
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
        Assert.Equal("/", login.Headers.Location?.ToString());
        var home = await client.GetStringAsync("/");
        Assert.Contains("aún no tiene acceso", home);
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
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), quote.FechaCreacion);
        Assert.Equal(236m, quote.Total);
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
    }

    [Fact]
    public async Task InternalDocumentsDefaultSevenDaysAndRevalidateWithoutChangingOriginal()
    {
        using var factory = CreateFactory();
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        int customerId, productId;
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = "print-admin@example.test", Email = "print-admin@example.test" };
            Assert.True((await manager.CreateAsync(user, "TestPassword123!")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customer = new Cliente { Nombre = "Cliente inicial", NumeroDocumento = "12345678", Direccion = "Dirección de prueba" };
            var product = new Producto { Nombre = "Artículo", Stock = 5, Precio = 10, CostoUnitarioSinIgv = 4 };
            db.AddRange(customer, product);
            await db.SaveChangesAsync();
            customerId = customer.IdCliente;
            productId = product.IdProducto;
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
            ["cotizacionId"] = newId.ToString(), ["__RequestVerificationToken"] = Token(details)
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
            context.AddRange(customer, item, service);
            await context.SaveChangesAsync();
            itemId = item.IdProducto;
            serviceId = service.IdProducto;

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
            Assert.Equal(3, (await context.Producto.FindAsync(itemId))!.Stock);
            Assert.Equal(0, (await context.Producto.FindAsync(serviceId))!.Stock);
            Assert.Equal(3, await context.AuditoriaOperacion.CountAsync(entry => entry.TipoEntidad == "Pedido" || entry.TipoEntidad == "Venta" || entry.TipoEntidad == "Cotizacion"));
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
            Assert.Equal(0, (await context.Producto.FindAsync(serviceId))!.Stock);

            var item = await context.Producto.FindAsync(itemId);
            item!.Stock = 1;
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
            ["__RequestVerificationToken"] = Token(insufficientDetails)
        }));
        Assert.Equal(HttpStatusCode.Redirect, rejectedConfirmation.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("Emitida", (await context.Cotizacion.FindAsync(quoteId))!.Estado);
            Assert.Equal(1, (await context.Producto.FindAsync(itemId))!.Stock);
            Assert.Single(await context.Venta.ToListAsync());
            Assert.Single(await context.Pedido.ToListAsync());

            var customer = await context.Cliente.SingleAsync();
            var seller = await context.Users.SingleAsync(user => user.Email == email);
            context.Cotizacion.Add(new Cotizacion
            {
                IdCliente = customer.IdCliente,
                IdUsuario = seller.Id,
                FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                FechaVencimiento = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                Estado = "Emitida",
                Total = 0m
            });
            await context.SaveChangesAsync();
        }

        var quotesIndex = await client.GetStringAsync("/Cotizacions");
        Assert.Contains("Vencida", quotesIndex);
        using var expirationScope = factory.Services.CreateScope();
        var expirationContext = expirationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Vencida", (await expirationContext.Cotizacion.OrderBy(item => item.IdCotizacion).LastAsync()).Estado);
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
        Assert.Single(await verifyContext.AuditoriaOperacion.Where(entry => entry.TipoEntidad == "Pedido").ToListAsync());
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
        Assert.Contains("[CORREO]", preview);
        Assert.Empty(fake.Textos);
        var match = Regex.Match(preview, "name=\"TextoRevisado\"[^>]*>(.*?)</textarea>", RegexOptions.Singleline);
        Assert.True(match.Success);
        var sanitized = WebUtility.HtmlDecode(match.Groups[1].Value);

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
