using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class GroqSolicitudExtractorTests
{
    [Fact]
    public void PrivacyPreviewRemovesKnownIdentifiersWithoutSendingAnyRequest()
    {
        var redacted = ProteccionSolicitudService.Anonimizar(
            "Necesito 100 volantes; correo: ana@example.test; teléfono 999 888 777; DNI: 12345678; RUC: 20123456789; dirección: Jr. Olivo 55");
        Assert.Contains("100 volantes", redacted);
        Assert.DoesNotContain("ana@example.test", redacted);
        Assert.DoesNotContain("999 888 777", redacted);
        Assert.DoesNotContain("12345678", redacted);
        Assert.DoesNotContain("20123456789", redacted);
        Assert.DoesNotContain("Olivo", redacted);
    }

    [Fact]
    public void CustomerIdentityCandidateIsExtractedLocallyAndNeverAddedBackToGroqText()
    {
        var request = "Cliente: Acme SAC; RUC: 20123456789; necesito 300 volantes A5";
        var candidate = ProteccionSolicitudService.ExtraerIdentidadLocal(request);
        var redacted = ProteccionSolicitudService.Anonimizar(request);

        Assert.Equal("Acme SAC", candidate.Nombre);
        Assert.Equal("6", candidate.CodigoDocumento);
        Assert.Equal("20123456789", candidate.NumeroDocumento);
        Assert.DoesNotContain(candidate.Nombre!, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(candidate.NumeroDocumento!, redacted, StringComparison.Ordinal);
        Assert.Contains("300 volantes", redacted);
    }

    [Fact]
    public async Task MissingKeyAndIdentifiableTextFailClosed()
    {
        var handler = new CaptureHandler();
        var missing = new GroqSolicitudExtractor(new HttpClient(handler), new ConfigurationBuilder().Build());
        Assert.False((await missing.ExtraerAsync("100 volantes a color", CancellationToken.None)).Exito);
        var configured = new GroqSolicitudExtractor(new HttpClient(handler), Configuration("testing-only"));
        Assert.False((await configured.ExtraerAsync("Correo ana@example.test: 100 volantes", CancellationToken.None)).Exito);
        Assert.Null(handler.Body);
    }

    [Fact]
    public async Task SendsStrictSchemaAndValidatesModelOutputWithoutLeakingCredentials()
    {
        var handler = new CaptureHandler
        {
            Response = """{"choices":[{"message":{"content":"{\"items\":[{\"nombre\":\"Papel\",\"cantidad\":2,\"especificacion\":\"A5\"}],\"observaciones\":\"Color\",\"fechaRequerida\":\"viernes\"}"}}]}"""
        };
        var service = new GroqSolicitudExtractor(new HttpClient(handler), Configuration("testing-only"));
        var result = await service.ExtraerAsync("Necesito 2 Papel A5 a color", CancellationToken.None);
        Assert.True(result.Exito, result.Mensaje);
        Assert.Equal(2, Assert.Single(result.Datos!.Lineas).Cantidad);
        Assert.Contains("json_schema", handler.Body);
        Assert.Contains("strict", handler.Body);
        Assert.Equal("testing-only", handler.Authorization);
        Assert.DoesNotContain("testing-only", handler.Body);

        handler.Response = """{"choices":[{"message":{"content":"{\"items\":[{\"nombre\":\"Papel\",\"cantidad\":-5,\"especificacion\":\"A5\"}],\"observaciones\":\"\",\"fechaRequerida\":\"\"}"}}]}""";
        Assert.False((await service.ExtraerAsync("Necesito papel A5", CancellationToken.None)).Exito);
        handler.Response = "no es JSON";
        Assert.False((await service.ExtraerAsync("Necesito papel A5", CancellationToken.None)).Exito);
    }

    [Fact]
    public async Task ProviderFailureFallsBackToManualQuote()
    {
        var service = new GroqSolicitudExtractor(new HttpClient(new TimeoutHandler()), Configuration("testing-only"));
        var result = await service.ExtraerAsync("Necesito 100 volantes a color", CancellationToken.None);
        Assert.False(result.Exito);
        Assert.Contains("cinco segundos", result.Mensaje);
    }

    private static IConfiguration Configuration(string apiKey) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Groq:ApiKey"] = apiKey }).Build();

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }
        public string Response { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Response, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new OperationCanceledException("simulated provider timeout");
    }
}
