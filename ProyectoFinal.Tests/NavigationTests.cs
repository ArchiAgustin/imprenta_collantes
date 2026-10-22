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
        var quoteForm = await client.GetStringAsync("/Cotizacions/Create");
        var quoteResponse = await client.PostAsync("/Cotizacions/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["IdCliente"] = customerId.ToString(),
            ["IdUsuario"] = "forged-id",
            ["FechaCreacion"] = "2000-01-01",
            ["Total"] = "42",
            ["__RequestVerificationToken"] = Token(quoteForm)
        }));
        Assert.Equal(HttpStatusCode.Redirect, quoteResponse.StatusCode);
        var quote = context.Cotizacion.Single();
        Assert.Equal(userId, quote.IdUsuario);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), quote.FechaCreacion);
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
