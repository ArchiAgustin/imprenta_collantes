using System.Data;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ResultadoInventario(bool Exito, string Mensaje, int? IdInventario = null, string? Numero = null);
public sealed record InventarioLineaInput(int IdProducto, decimal? CantidadBase);

public sealed class InventarioService(ApplicationDbContext db)
{
    public const string NombreAlmacen = "Almacén principal";

    // A transaction-scoped application lock serializes balances, cuts, transfers,
    // sales and product creation across warehouses.
    public async Task BloquearInventarioAsync()
    {
        if (!db.Database.IsRelational()) return;
        var result = await db.Database.SqlQueryRaw<int>(@"
DECLARE @result int;
EXEC @result = sp_getapplock @Resource = N'ProyectoFinal.Inventory.AllWarehouses',
    @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
SELECT @result AS [Value];").ToListAsync();
        if (result.Single() < 0) throw new TimeoutException("No se pudo bloquear el inventario para esta operación.");
    }

    public async Task<Almacen?> BloquearAlmacenAsync(int? idAlmacen = null)
    {
        await BloquearInventarioAsync();
        var query = db.Almacenes.Where(item => item.Activo);
        query = idAlmacen is { } id
            ? query.Where(item => item.IdAlmacen == id)
            : query.Where(item => item.EsPredeterminado || item.Nombre == NombreAlmacen);
        return db.Database.IsRelational()
            ? await query.AsNoTracking().SingleOrDefaultAsync()
            : await query.SingleOrDefaultAsync();
    }

    public Task<Almacen?> BloquearPrincipalAsync() => BloquearAlmacenAsync();

    public async Task<ResultadoInventario> CrearInventarioAsync(
        int idAlmacen,
        DateOnly fecha,
        IReadOnlyList<InventarioLineaInput> lineas,
        string? usuario,
        System.Security.Claims.ClaimsPrincipal actor)
    {
        const decimal maximo = 999_999_999_999_999.999m;
        if (lineas.Count == 0 || lineas.Select(item => item.IdProducto).Distinct().Count() != lineas.Count
            || lineas.Any(item => item.CantidadBase is null or < 0 or > maximo
                || item.CantidadBase is { } amount && decimal.Round(amount, 3) != amount))
            return new(false, "Agrega al menos un producto y una cantidad válida; se permiten hasta tres decimales y cero.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        await BloquearInventarioAsync();

        var almacen = await db.Almacenes.FirstOrDefaultAsync(item => item.IdAlmacen == idAlmacen && item.Activo);
        if (almacen is null) return new(false, "Selecciona un almacén activo.");
        var initial = almacen.FechaActivacionUtc is null;
        var cantidades = lineas.ToDictionary(item => item.IdProducto, item => item.CantidadBase!.Value);
        var selectedProductIds = cantidades.Keys.ToArray();
        var productos = await db.Producto.AsNoTracking()
            .Where(item => selectedProductIds.Contains(item.IdProducto) && !item.EsServicio)
            .ToDictionaryAsync(item => item.IdProducto);
        if (productos.Count != cantidades.Count)
            return new(false, "Uno de los productos ya no existe o no corresponde a un artículo con stock.");
        var allArticles = await db.Producto.AsNoTracking().Where(item => !item.EsServicio)
            .Select(item => new { item.IdProducto, item.IdUnidadMedidaBase }).ToListAsync();
        var activeUnitIds = (await db.UnidadesMedida.AsNoTracking().Where(item => item.Activo)
            .Select(item => item.IdUnidadMedida).ToListAsync()).ToHashSet();
        if (allArticles.Any(item => item.IdUnidadMedidaBase is not { } unitId || !activeUnitIds.Contains(unitId)))
            return new(false, "Hay artículos sin una unidad base activa. Configúrala antes de crear el inventario.");
        if (initial && !allArticles.Select(item => item.IdProducto).ToHashSet().SetEquals(cantidades.Keys))
            return new(false, "La apertura inicial requiere contar todos los artículos del almacén, aunque su existencia física sea cero.");

        var serie = await db.SeriesInventario.SingleOrDefaultAsync(item => item.Activa);
        if (serie is null) return new(false, "No hay una serie de inventario activa.");
        if (serie.SiguienteCorrelativo >= 99_999_999)
            return new(false, "La serie INV1 llegó al máximo correlativo de ocho dígitos.");

        // On an opening count all articles are mandatory; on later counts omitted
        // products keep their current balances.
        var allProductIds = allArticles.Select(item => item.IdProducto).ToArray();
        var balances = await db.SaldosAlmacen
            .Where(item => item.IdAlmacen == idAlmacen && allProductIds.Contains(item.IdProducto))
            .ToDictionaryAsync(item => item.IdProducto);
        foreach (var productId in allProductIds.Where(id => !balances.ContainsKey(id)))
        {
            var balance = new StockAlmacen { IdAlmacen = idAlmacen, IdProducto = productId, CantidadBase = 0m };
            db.SaldosAlmacen.Add(balance);
            balances.Add(productId, balance);
        }

        var createdUtc = DateTimeOffset.UtcNow;
        var correlativo = serie.SiguienteCorrelativo;
        var inventario = new Inventario
        {
            Serie = serie.Serie,
            Correlativo = correlativo,
            Fecha = fecha,
            CreadoUtc = createdUtc,
            IdAlmacen = idAlmacen,
            IdUsuario = usuario
        };
        var movements = new List<(int ProductId, decimal Previous, decimal Current)>();
        foreach (var (productId, current) in cantidades)
        {
            var balance = balances[productId];
            var previous = balance.CantidadBase;
            balance.CantidadBase = current;
            inventario.Detalles.Add(new InventarioDetalle
            {
                IdProducto = productId,
                CantidadSistemaBase = previous,
                CantidadContadaBase = current
            });
            if (previous != current) movements.Add((productId, previous, current));
        }

        db.Inventarios.Add(inventario);
        serie.SiguienteCorrelativo++;
        if (initial) almacen.FechaActivacionUtc = createdUtc;
        almacen.ConteoEnCurso = false;
        await db.SaveChangesAsync();

        foreach (var (productId, previous, current) in movements)
        {
            db.Kardex.Add(new Kardex
            {
                IdAlmacen = idAlmacen,
                IdProducto = productId,
                FechaUtc = createdUtc,
                TipoMovimiento = initial ? "Corte inicial" : current > previous ? "Ajuste entrada" : "Ajuste salida",
                CantidadBase = Math.Abs(current - previous),
                SaldoAnteriorBase = previous,
                SaldoPosteriorBase = current,
                TipoReferencia = "Inventario",
                IdReferencia = inventario.IdInventario,
                IdUsuario = usuario,
                Observaciones = $"Inventario {inventario.Numero}; stock físico reemplazado."
            });
        }

        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(actor, "Inventario", inventario.IdInventario,
            "Inventario aplicado", null, "Aplicado",
            $"{inventario.Numero}; almacén {almacen.Nombre}; {cantidades.Count} productos; {fecha:yyyy-MM-dd}."));
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, $"Inventario {inventario.Numero} registrado en «{almacen.Nombre}» con {cantidades.Count} productos.", inventario.IdInventario, inventario.Numero);
    }
}
