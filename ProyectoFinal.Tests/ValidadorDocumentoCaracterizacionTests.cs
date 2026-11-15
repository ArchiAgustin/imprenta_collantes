using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public sealed class ValidadorDocumentoCaracterizacionTests
{
    [Fact]
    public async Task DniDeOchoDigitosEsValido()
    {
        using var db = PrepararBase();
        var errors = new ModelStateDictionary();
        var (tipo, numero) = await new IdentificacionService(db).ValidarClienteAsync(
            NuevoCliente(1, "12345678"), null, errors);

        Assert.Equal("1", tipo?.Codigo);
        Assert.Equal("12345678", numero);
        Assert.True(errors.IsValid);
    }

    [Fact]
    public async Task DniDeSieteDigitosMantieneElMensajeDeError()
    {
        using var db = PrepararBase();
        var errors = new ModelStateDictionary();
        await new IdentificacionService(db).ValidarClienteAsync(NuevoCliente(1, "1234567"), null, errors);

        Assert.Equal("Ingresa un DNI de 8 dígitos.", errors[nameof(Cliente.NumeroDocumento)]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task RucDeOnceDigitosEsValidoConDireccionYUbigeo()
    {
        using var db = PrepararBase();
        var errors = new ModelStateDictionary();
        var (tipo, numero) = await new IdentificacionService(db).ValidarClienteAsync(
            NuevoCliente(6, "20123456789"), null, errors);

        Assert.Equal("6", tipo?.Codigo);
        Assert.Equal("20123456789", numero);
        Assert.True(errors.IsValid);
    }

    [Fact]
    public async Task RucDeDiezDigitosMantieneElMensajeDeError()
    {
        using var db = PrepararBase();
        var errors = new ModelStateDictionary();
        await new IdentificacionService(db).ValidarClienteAsync(NuevoCliente(6, "2012345678"), null, errors);

        Assert.Equal("Ingresa un RUC de 11 dígitos.", errors[nameof(Cliente.NumeroDocumento)]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task NumeroConEspaciosSeNormalizaAntesDeValidar()
    {
        using var db = PrepararBase();
        var errors = new ModelStateDictionary();
        var cliente = NuevoCliente(1, " 12345678 ");
        var (_, numero) = await new IdentificacionService(db).ValidarClienteAsync(cliente, null, errors);

        Assert.True(errors.IsValid);
        Assert.Equal("12345678", numero);
        Assert.Equal("12345678", cliente.NumeroDocumento);
    }

    private static Cliente NuevoCliente(int tipo, string numero) => new()
    {
        Nombre = "Cliente de prueba",
        IdDocumentoIdentidadTipo = tipo,
        NumeroDocumento = numero,
        Direccion = "Av. Ejemplo 123",
        UbigeoCodigo = tipo == 6 ? "150101" : null
    };

    private static ApplicationDbContext PrepararBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ApplicationDbContext(options);
        db.DocumentoIdentidadTipos.AddRange(
            new DocumentoIdentidadTipo { IdDocumentoIdentidadTipo = 1, Codigo = "1", Nombre = "DNI", Estado = true },
            new DocumentoIdentidadTipo { IdDocumentoIdentidadTipo = 6, Codigo = "6", Nombre = "RUC", Estado = true });
        db.Ubigeos.AddRange(
            new Ubigeo { IdUbigeo = 1, Codigo = "150000", Descripcion = "Lima", Parent = "000000", IdParent = 0 },
            new Ubigeo { IdUbigeo = 2, Codigo = "150100", Descripcion = "Lima", Parent = "150000", IdParent = 1 },
            new Ubigeo { IdUbigeo = 3, Codigo = "150101", Descripcion = "Lima", Parent = "150100", IdParent = 2 });
        db.SaveChanges();
        return db;
    }
}
