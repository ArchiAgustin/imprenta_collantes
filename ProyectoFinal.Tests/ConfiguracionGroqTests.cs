using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProyectoFinal.Data;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public class ConfiguracionGroqTests
{
    [Fact]
    public async Task GuardaClaveCifradaYPermiteReemplazarlaSinExponerla()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new ApplicationDbContext(options);
        var provider = DataProtectionProvider.Create("groq-tests");
        var config = new ConfigurationBuilder().Build();
        var service = new ConfiguracionGroqService(db, provider, config);

        Assert.False(await service.TieneClaveAsync());
        await service.GuardarClaveAsync("testing-key-one");
        Assert.True(await service.TieneClaveAsync());
        Assert.NotEqual("testing-key-one", (await db.ConfiguracionGroq.SingleAsync()).ApiKeyProtegida);
        Assert.Equal("testing-key-one", await service.ObtenerClaveAsync());

        await service.GuardarClaveAsync("testing-key-two");
        Assert.Equal(1, await db.ConfiguracionGroq.CountAsync());
        Assert.Equal("testing-key-two", await service.ObtenerClaveAsync());
    }

    [Fact]
    public async Task SinClaveEnBaseUsaConfiguracionAnteriorYNoRecuperaClaveConOtroProtector()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new ApplicationDbContext(options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Groq:ApiKey"] = "testing-fallback" }).Build();
        var service = new ConfiguracionGroqService(db, DataProtectionProvider.Create("provider-one"), config);
        Assert.Equal("testing-fallback", await service.ObtenerClaveAsync());
        await service.GuardarClaveAsync("testing-stored");
        Assert.Equal("testing-stored", await service.ObtenerClaveAsync());

        var wrongKeys = new ConfiguracionGroqService(db, DataProtectionProvider.Create("provider-two"), config);
        Assert.Null(await wrongKeys.ObtenerClaveAsync());
    }
}
