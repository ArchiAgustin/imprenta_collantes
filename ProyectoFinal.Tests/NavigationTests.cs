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
using Xunit;

namespace ProyectoFinal.Tests;

public class NavigationTests
{
    private static WebApplicationFactory<Program> CreateFactory()
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
            });
        });
    }

    [Theory]
    [InlineData("/Clientes")]
    [InlineData("/Proveedors")]
    [InlineData("/Productoes")]
    [InlineData("/Cotizacions")]
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
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
        var users = await client.GetStringAsync("/Admin");
        Assert.Contains("Roles asignados", users);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Roles")).StatusCode);
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
            seedContext.Producto.Add(new Producto { Nombre = "Impresión de prueba", Precio = 100m, Stock = 10 });
            await seedContext.SaveChangesAsync();
        }
        await Login(client, "owner@example.test");
        var customerForm = await client.GetStringAsync("/Clientes/Create");
        var customerResponse = await client.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Nombre"] = "Cliente de prueba",
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
