using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public sealed class SerieSunatTypeTests
{
    [Fact]
    public async Task SalesSeriesCanReferenceAnActiveSunatTypeWithoutChangingItsInternalNumbering()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var receiptType = new TipoDocumentoPagoSunat
        {
            IdTipoDocumentoPago = 3, CodigoSunat = "03", Descripcion = "Boleta de venta", Estado = true
        };
        db.TiposDocumentoPagoSunat.Add(receiptType);
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "admin") }, "Test"));
        var service = new SerieDocumentoInternoService(db);

        Assert.False((await service.GuardarAsync(null, SerieDocumentoInternoService.Venta, "V002", 1, 6, true, actor)).Exito);
        var saved = await service.GuardarAsync(null, SerieDocumentoInternoService.Venta, "V002", 1, 6, true, actor,
            receiptType.IdTipoDocumentoPago);

        Assert.True(saved.Exito, saved.Mensaje);
        var series = await db.SeriesDocumentoInterno.Include(item => item.TipoComprobanteSunat).SingleAsync();
        Assert.Equal("V002", series.Serie);
        Assert.Equal(1, series.SiguienteCorrelativo);
        Assert.Equal(6, series.Digitos);
        Assert.Equal(receiptType.IdTipoDocumentoPago, series.IdTipoComprobanteSunat);
        Assert.Equal("03", series.TipoComprobanteSunat!.CodigoSunat);
    }

    [Fact]
    public async Task QuotationsCannotBeAssignedPaymentReceiptTypesAndInactiveTypesAreRejected()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        db.TiposDocumentoPagoSunat.Add(new TipoDocumentoPagoSunat
        {
            IdTipoDocumentoPago = 1, CodigoSunat = "01", Descripcion = "Factura", Estado = false
        });
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity());
        var service = new SerieDocumentoInternoService(db);

        var quoteResult = await service.GuardarAsync(null, SerieDocumentoInternoService.Cotizacion, "C002", 1, 6, true, actor, 1);
        var inactiveResult = await service.GuardarAsync(null, SerieDocumentoInternoService.Venta, "V002", 1, 6, true, actor, 1);

        Assert.False(quoteResult.Exito);
        Assert.False(inactiveResult.Exito);
        Assert.Empty(await db.SeriesDocumentoInterno.ToListAsync());
    }
}
