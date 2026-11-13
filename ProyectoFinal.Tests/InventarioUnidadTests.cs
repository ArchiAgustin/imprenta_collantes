using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;
using Xunit;

namespace ProyectoFinal.Tests;

public sealed class InventarioUnidadTests
{
    [Fact]
    public async Task NewWarehouseOpeningMustCountEveryArticleAndAcceptExplicitZero()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var unit = new UnidadMedida { Descripcion = "Pliego", DescripcionCorta = "plg", Activo = true };
        var first = new Producto { Nombre = "Cartulina", EsServicio = false, IdUnidadMedidaBase = 1, UnidadMedidaBase = unit };
        var second = new Producto { Nombre = "Papel", EsServicio = false, IdUnidadMedidaBase = 1, UnidadMedidaBase = unit };
        var warehouse = new Almacen { Nombre = "Almacén nuevo", Activo = true };
        var series = new SerieInventario { Serie = "INV1", Activa = true, SiguienteCorrelativo = 1, Digitos = 8 };
        db.AddRange(unit, first, second, warehouse, series);
        await db.SaveChangesAsync();
        first.IdUnidadMedidaBase = unit.IdUnidadMedida;
        second.IdUnidadMedidaBase = unit.IdUnidadMedida;
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "inventory-admin")
        }, "test"));
        var service = new InventarioService(db);

        var incomplete = await service.CrearInventarioAsync(warehouse.IdAlmacen, new DateOnly(2026, 11, 5),
            new[] { new InventarioLineaInput(first.IdProducto, 3m) }, "inventory-admin", actor);

        Assert.False(incomplete.Exito);
        Assert.Contains("todos los artículos", incomplete.Mensaje);
        Assert.Null((await db.Almacenes.SingleAsync()).FechaActivacionUtc);
        Assert.Empty(await db.Inventarios.ToListAsync());
        Assert.Empty(await db.SaldosAlmacen.ToListAsync());
        Assert.Equal(1, (await db.SeriesInventario.SingleAsync()).SiguienteCorrelativo);

        var complete = await service.CrearInventarioAsync(warehouse.IdAlmacen, new DateOnly(2026, 11, 5),
            new[]
            {
                new InventarioLineaInput(first.IdProducto, 0m),
                new InventarioLineaInput(second.IdProducto, 4.125m)
            }, "inventory-admin", actor);

        Assert.True(complete.Exito, complete.Mensaje);
        Assert.Equal(2, (await db.Inventarios.SingleAsync()).Detalles.Count);
        Assert.Equal(0m, (await db.SaldosAlmacen.FindAsync(warehouse.IdAlmacen, first.IdProducto))!.CantidadBase);
        Assert.Equal(4.125m, (await db.SaldosAlmacen.FindAsync(warehouse.IdAlmacen, second.IdProducto))!.CantidadBase);
        Assert.Equal(1, await db.Kardex.CountAsync());
        Assert.Single(await db.AuditoriaOperacion.Where(item => item.TipoEntidad == "Inventario").ToListAsync());
    }
}
