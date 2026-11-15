using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProyectoFinal.Data;
using Xunit;

namespace ProyectoFinal.Tests;

public sealed class ProductionExceptionTests
{
    [Fact]
    public async Task ProductionExceptionShowsFriendlyPageAndLogsTheExceptionWithoutExposingIt()
    {
        const string marker = "CONTROLLED_EXCEPTION_SECRET_7B3F";
        var logs = new CapturingLoggerProvider();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "ProyectoFinal.Tests.Keys", Environment.ProcessId.ToString()));
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;TrustServerCertificate=True",
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "ProyectoFinal.Tests.Keys", Environment.ProcessId.ToString()),
                ["BootstrapAdmin:Email"] = "",
                ["BootstrapAdmin:Password"] = ""
            }));
            builder.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddProvider(logs); });
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<KeyManagementOptions>(options => options.XmlEncryptor = null);
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
                services.AddControllersWithViews().AddApplicationPart(typeof(ControlledExceptionController).Assembly);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/__tests/error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Código de solicitud", body);
        Assert.Contains("No pudimos completar la solicitud", body);
        Assert.DoesNotContain(marker, body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at ProyectoFinal.Tests", body, StringComparison.Ordinal);
        Assert.Contains(logs.Messages, message => message.Contains(marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task IdentityFormsAndHttpErrorsShowSpanishMessages()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "ProyectoFinal.Tests.Keys", Environment.ProcessId.ToString()));
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;TrustServerCertificate=True",
                ["BootstrapAdmin:Email"] = "",
                ["BootstrapAdmin:Password"] = ""
            }));
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<KeyManagementOptions>(options => options.XmlEncryptor = null);
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = WebUtility.HtmlDecode(await client.GetStringAsync("/Identity/Account/Login"));
        var metadata = factory.Services.GetRequiredService<IModelMetadataProvider>().GetMetadataForProperty(
            typeof(ProyectoFinal.Areas.Identity.Pages.Account.LoginModel.InputModel), "Email");
        Assert.Equal("Ingresa un correo electrónico válido.", metadata.ValidatorMetadata.OfType<EmailAddressAttribute>().Single().ErrorMessage);
        Assert.Contains("El campo Correo electrónico es obligatorio.", login);
        Assert.Contains("Ingresa un correo electrónico válido.", login);
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var invalidLogin = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = "correo-invalido"
        }));
        Assert.Equal(HttpStatusCode.OK, invalidLogin.StatusCode);
        var validation = WebUtility.HtmlDecode(await invalidLogin.Content.ReadAsStringAsync());
        Assert.Contains("Ingresa un correo electrónico válido.", validation);
        Assert.Contains("El campo Contraseña es obligatorio.", validation);

        var registrationMetadata = factory.Services.GetRequiredService<IModelMetadataProvider>().GetMetadataForProperty(
            typeof(ProyectoFinal.Areas.Identity.Pages.Account.RegisterModel.InputModel), "Password");
        var passwordLength = registrationMetadata.ValidatorMetadata.OfType<StringLengthAttribute>().Single();
        Assert.Equal("La contraseña debe tener entre 6 y 100 caracteres.", passwordLength.FormatErrorMessage("Contraseña"));
        var portMetadata = factory.Services.GetRequiredService<IModelMetadataProvider>().GetMetadataForProperty(
            typeof(ProyectoFinal.Models.ParametrosCorreoViewModel), "Puerto");
        Assert.Equal("El campo Puerto debe estar entre 1 y 65535.",
            portMetadata.ValidatorMetadata.OfType<RangeAttribute>().Single().FormatErrorMessage("Puerto"));
        var quoteMetadata = factory.Services.GetRequiredService<IModelMetadataProvider>().GetMetadataForProperty(
            typeof(ProyectoFinal.Models.CotizacionFormViewModel), "ClienteId");
        Assert.Equal("Selecciona un cliente válido.",
            quoteMetadata.ValidatorMetadata.OfType<RangeAttribute>().Single().FormatErrorMessage("ClienteId"));

        var notFound = await client.GetAsync("/pagina-que-no-existe");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Contains("No se encontró la página solicitada", await notFound.Content.ReadAsStringAsync());

        var denied = await client.GetStringAsync("/Identity/Account/AccessDenied");
        Assert.Contains("No tienes permiso para acceder", denied);

        var errors = factory.Services.GetRequiredService<IdentityErrorDescriber>();
        Assert.Contains("contraseña", errors.PasswordTooShort(6).Description);
        Assert.Contains("correo electrónico", errors.DuplicateEmail("test@example.com").Description);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                var text = formatter(state, exception);
                if (exception is not null) text += " " + exception;
                messages.Enqueue(text);
            }
        }
    }
}

[ApiController]
[Route("__tests/error")]
public sealed class ControlledExceptionController : ControllerBase
{
    [HttpGet]
    public IActionResult Throw() => throw new InvalidOperationException("CONTROLLED_EXCEPTION_SECRET_7B3F");
}
