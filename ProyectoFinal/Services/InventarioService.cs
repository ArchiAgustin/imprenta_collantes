using System.Data;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed record ResultadoInventario(bool Exito, string Mensaje, int? IdCorte = null);
public sealed record ConteoLineaInput(int IdProducto, decimal? CantidadBase);

public sealed class InventarioService(ApplicationDbContext db)
{
    public const string NombreAlmacen = "Almacén principal";

    // A transaction-scoped application lock serializes count/cutover, sales, returns,
    // receipts and product creation without SQL Server index-key update deadlocks.
    public async Task<Almacen?> BloquearPrincipalAsync()
    {
        if (db.Database.IsRelational())
        {
            var result = await db.Database.SqlQueryRaw<int>(@"
DECLARE @result int;
EXEC @result = sp_getapplock @Resource = N'ProyectoFinal.Inventory.Principal',
    @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
SELECT @result AS [Value];").ToListAsync();
            if (result.Single() < 0) throw new TimeoutException("No se pudo bloquear el inventario para esta operación.");
            return await db.Almacenes.AsNoTracking().SingleOrDefaultAsync(item => item.Nombre == NombreAlmacen && item.Activo);
        }
        return await db.Almacenes.SingleOrDefaultAsync(item => item.Nombre == NombreAlmacen && item.Activo);
    }

    public async Task<ResultadoInventario> IniciarConteoAsync(string? usuario)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        var almacen = await BloquearPrincipalAsync();
        if (almacen is null) return new(false, "No existe el Almacén principal.");
        if (almacen.ConteoEnCurso) return new(false, "Ya hay un conteo en curso. Termínalo o anúlalo antes de iniciar otro.");
        var products = await db.Producto.AsNoTracking().Where(item => !item.EsServicio).OrderBy(item => item.IdProducto).ToListAsync();
        if (products.Count == 0) return new(false, "Registra un artículo antes de iniciar el conteo.");
        if (products.Any(item => item.IdUnidadMedidaBase is null))
            return new(false, "Hay artículos sin unidad base. Configúralos antes de contar.");

        var corte = new CorteInventario
        {
            IdAlmacen = almacen.IdAlmacen,
            FechaUtc = DateTimeOffset.UtcNow,
            IdUsuario = usuario,
            Estado = "Borrador"
        };
        foreach (var product in products)
        {
            var balance = almacen.FechaActivacionUtc is null ? null : await db.SaldosAlmacen.AsNoTracking()
                .FirstOrDefaultAsync(item => item.IdAlmacen == almacen.IdAlmacen && item.IdProducto == product.IdProducto);
            if (almacen.FechaActivacionUtc is not null && balance is null)
                return new(false, $"Falta el saldo de «{product.Nombre}»; no se inició el conteo.");
            corte.Detalles.Add(new CorteInventarioDetalle
            {
                IdProducto = product.IdProducto,
                CantidadSistemaBase = balance?.CantidadBase,
                ConteoRegistrado = false
            });
        }
        db.CortesInventario.Add(corte);
        if (db.Database.IsRelational())
            await db.Almacenes.Where(item => item.IdAlmacen == almacen.IdAlmacen)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.ConteoEnCurso, true));
        else almacen.ConteoEnCurso = true;
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, "Conteo iniciado. Las ventas y devoluciones se pausaron hasta aprobarlo o anularlo.", corte.IdCorteInventario);
    }

    public async Task<ResultadoInventario> GuardarConteoAsync(int id, IReadOnlyList<ConteoLineaInput> lineas)
    {
        if (lineas.Count == 0 || lineas.Select(item => item.IdProducto).Distinct().Count() != lineas.Count
            || lineas.Any(item => item.CantidadBase is < 0 or > 999_999_999_999_999.999m
                || item.CantidadBase is { } amount && decimal.Round(amount, 3) != amount))
            return new(false, "Revisa las cantidades: no deben ser negativas y admiten hasta tres decimales.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        var almacen = await BloquearPrincipalAsync();
        var corte = await db.CortesInventario.Include(item => item.Detalles)
            .FirstOrDefaultAsync(item => item.IdCorteInventario == id);
        if (almacen is null || !almacen.ConteoEnCurso || corte is null || corte.IdAlmacen != almacen.IdAlmacen || corte.Estado != "Borrador")
            return new(false, "El conteo ya no está abierto. Recarga la página.");
        var details = corte.Detalles.ToDictionary(item => item.IdProducto);
        if (lineas.Any(item => !details.ContainsKey(item.IdProducto)))
            return new(false, "El conteo incluye un artículo que no pertenece al corte.");
        foreach (var line in lineas.Where(item => item.CantidadBase.HasValue))
        {
            var detail = details[line.IdProducto];
            detail.CantidadContadaBase = line.CantidadBase!.Value;
            detail.ConteoRegistrado = true; // Explicit zero is a recorded physical count.
        }
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, "Conteo guardado; aún no se modificaron existencias.", id);
    }

    public async Task<ResultadoInventario> FinalizarConteoAsync(int id, string? usuario, bool aprobar)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        var almacen = await BloquearPrincipalAsync();
        var corte = await db.CortesInventario.Include(item => item.Detalles)
            .FirstOrDefaultAsync(item => item.IdCorteInventario == id);
        if (almacen is null || !almacen.ConteoEnCurso || corte is null || corte.Estado != "Borrador" || corte.IdAlmacen != almacen.IdAlmacen)
            return new(false, "El conteo ya fue cerrado o no corresponde al almacén.");
        if (!aprobar)
        {
            corte.Estado = "Anulado";
            await CambiarEstadoConteoAsync(almacen, false);
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return new(true, "Conteo anulado; se reanudaron las ventas sin cambiar existencias.", id);
        }

        var ids = await db.Producto.AsNoTracking().Where(item => !item.EsServicio)
            .Select(item => item.IdProducto).ToListAsync();
        if (ids.Count != corte.Detalles.Count || corte.Detalles.Any(item => !ids.Contains(item.IdProducto) || !item.ConteoRegistrado))
            return new(false, "Faltan artículos por contar. Ingresa incluso los que tienen cantidad cero.");
        var apertura = almacen.FechaActivacionUtc is null;
        var fecha = DateTimeOffset.UtcNow;
        foreach (var item in corte.Detalles.OrderBy(item => item.IdProducto))
        {
            var balance = await db.SaldosAlmacen.FindAsync(almacen.IdAlmacen, item.IdProducto);
            if (apertura && balance is not null) return new(false, "El almacén ya contiene saldos. No se duplicó la apertura.");
            if (!apertura && (balance is null || balance.CantidadBase != item.CantidadSistemaBase))
                return new(false, "Un saldo cambió durante el conteo. No se aplicaron ajustes.");

            var anterior = balance?.CantidadBase ?? 0m;
            var actual = item.CantidadContadaBase;
            if (apertura) db.SaldosAlmacen.Add(new StockAlmacen { IdAlmacen = almacen.IdAlmacen, IdProducto = item.IdProducto, CantidadBase = actual });
            else balance!.CantidadBase = actual;
            if (actual != anterior)
                db.Kardex.Add(new Kardex
                {
                    IdAlmacen = almacen.IdAlmacen, IdProducto = item.IdProducto, FechaUtc = fecha,
                    TipoMovimiento = apertura ? "Apertura" : actual > anterior ? "Ajuste entrada" : "Ajuste salida",
                    CantidadBase = Math.Abs(actual - anterior), SaldoAnteriorBase = anterior, SaldoPosteriorBase = actual,
                    TipoReferencia = "CorteInventario", IdReferencia = id, IdUsuario = usuario,
                    Observaciones = apertura ? "Saldo de apertura según conteo físico; no corresponde a compras históricas." : "Diferencia de conteo físico."
                });
        }
        corte.Estado = "Aprobado";
        corte.FechaAprobacionUtc = fecha;
        if (apertura)
        {
            if (db.Database.IsRelational())
                await db.Almacenes.Where(item => item.IdAlmacen == almacen.IdAlmacen)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.FechaActivacionUtc, fecha));
            else almacen.FechaActivacionUtc = fecha;
        }
        await CambiarEstadoConteoAsync(almacen, false);
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, apertura ? "Apertura aprobada. Desde ahora las ventas descuentan del Almacén principal." : "Corte aplicado y diferencias registradas en kárdex.", id);
    }

    public async Task<ResultadoInventario> RegistrarEntradaAsync(int productId, decimal cantidad, string motivo, string? usuario)
    {
        if (cantidad <= 0 || cantidad > 999_999_999_999_999.999m || decimal.Round(cantidad, 3) != cantidad
            || string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 500)
            return new(false, "Indica una cantidad positiva (hasta tres decimales) y un motivo de hasta 500 caracteres.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
        var almacen = await BloquearPrincipalAsync();
        if (almacen?.FechaActivacionUtc is null || almacen.ConteoEnCurso)
            return new(false, "Primero aprueba el conteo inicial o espera a que termine el corte en curso.");
        var product = await db.Producto.AsNoTracking().FirstOrDefaultAsync(item => item.IdProducto == productId && !item.EsServicio);
        var balance = await db.SaldosAlmacen.FindAsync(almacen.IdAlmacen, productId);
        if (product is null || balance is null) return new(false, "No se encontró el artículo en el almacén activo.");
        var anterior = balance.CantidadBase;
        if (anterior + cantidad > 999_999_999_999_999.999m) return new(false, "La existencia excedería el límite permitido.");
        balance.CantidadBase = anterior + cantidad;
        db.Kardex.Add(new Kardex
        {
            IdAlmacen = almacen.IdAlmacen, IdProducto = productId, FechaUtc = DateTimeOffset.UtcNow,
            TipoMovimiento = "Entrada", CantidadBase = cantidad, SaldoAnteriorBase = anterior,
            SaldoPosteriorBase = balance.CantidadBase, IdUsuario = usuario, Observaciones = motivo.Trim()
        });
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, "Entrada registrada en stock y kárdex.");
    }

    private async Task CambiarEstadoConteoAsync(Almacen almacen, bool valor)
    {
        if (db.Database.IsRelational())
            await db.Almacenes.Where(item => item.IdAlmacen == almacen.IdAlmacen)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.ConteoEnCurso, valor));
        else almacen.ConteoEnCurso = valor;
    }
}
