using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
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
        Assert.Contains("Request ID", body);
        Assert.DoesNotContain(marker, body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at ProyectoFinal.Tests", body, StringComparison.Ordinal);
        Assert.Contains(logs.Messages, message => message.Contains(marker, StringComparison.Ordinal));
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
