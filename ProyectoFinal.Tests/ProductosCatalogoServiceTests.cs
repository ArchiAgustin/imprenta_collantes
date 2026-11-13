using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public sealed class ProductosCatalogoServiceTests
{
    [Fact]
    public async Task CreateEditAndDeleteRespectCommercialReferences()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var service = new ProductosCatalogoService(db, new InventarioService(db));
        var product = new Producto { Nombre = "Servicio de prueba", EsServicio = true, Precio = 50m, Stock = 100,
            RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 } };
        Assert.Equal(ResultadoProductoEstado.Exito, (await service.CrearAsync(product)).Estado);
        Assert.Equal(0, product.Stock);
        Assert.NotNull(await service.ObtenerAsync(product.IdProducto));
        db.ChangeTracker.Clear();
        var updated = new Producto
        {
            IdProducto = product.IdProducto, Nombre = "Servicio editado", EsServicio = true,
            Precio = 60m, RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
        };
        Assert.Equal(ResultadoProductoEstado.Exito, (await service.ActualizarAsync(product.IdProducto, updated)).Estado);
        Assert.Equal("Servicio editado", (await service.ObtenerAsync(product.IdProducto))?.Nombre);

        var quoted = new DetalleCotizacion { IdProducto = product.IdProducto };
        db.Set<DetalleCotizacion>().Add(quoted);
        await db.SaveChangesAsync();
        Assert.Equal(ResultadoProductoEstado.EnUso, (await service.EliminarAsync(product.IdProducto)).Estado);
        db.Set<DetalleCotizacion>().Remove(quoted);
        await db.SaveChangesAsync();
        Assert.Equal(ResultadoProductoEstado.Exito, (await service.EliminarAsync(product.IdProducto)).Estado);
        Assert.Null(await service.ObtenerAsync(product.IdProducto));
    }
}
