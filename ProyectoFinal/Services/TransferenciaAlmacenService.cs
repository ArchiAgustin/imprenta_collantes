using System.Data;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class TransferenciaAlmacenService(ApplicationDbContext db, InventarioService inventario) : ITransferenciaAlmacenService
{
    public async Task<TransferenciasInventarioViewModel> ObtenerPanelAsync() => new()
    {
        Formulario = new TransferenciaAlmacenViewModel
        {
            Almacenes = await db.Almacenes.AsNoTracking()
                .Where(item => item.Activo && item.FechaActivacionUtc != null)
                .OrderBy(item => item.Nombre).ToListAsync(),
            Productos = await db.Producto.AsNoTracking().Where(item => !item.EsServicio)
                .OrderBy(item => item.Nombre).ToListAsync()
        },
        Historial = await db.TransferenciasAlmacen.AsNoTracking()
            .Include(item => item.AlmacenOrigen).Include(item => item.AlmacenDestino).Include(item => item.Producto)
            .OrderByDescending(item => item.IdTransferenciaAlmacen).Take(100).ToListAsync()
    };

    public async Task<ResultadoTransferenciaInventario> TransferirAsync(
        int idOrigen,
        int idDestino,
        int idProducto,
        decimal cantidad,
        string motivo,
        string? usuario,
        System.Security.Claims.ClaimsPrincipal actor)
    {
        if (idOrigen == idDestino) return new(false, "El almacén de origen y destino deben ser distintos.");
        if (cantidad <= 0 || cantidad > 999_999_999_999_999.999m || decimal.Round(cantidad, 3) != cantidad)
            return new(false, "La cantidad debe ser positiva y admitir como máximo tres decimales.");
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 500)
            return new(false, "Indica un motivo de hasta 500 caracteres.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)
            : null;
        await inventario.BloquearInventarioAsync();

        var warehouses = await db.Almacenes.AsNoTracking()
            .Where(item => item.Activo && item.FechaActivacionUtc != null
                && (item.IdAlmacen == idOrigen || item.IdAlmacen == idDestino))
            .ToListAsync();
        var source = warehouses.SingleOrDefault(item => item.IdAlmacen == idOrigen);
        var destination = warehouses.SingleOrDefault(item => item.IdAlmacen == idDestino);
        if (source is null || destination is null)
            return await FailAsync(transaction, "Ambos almacenes deben estar activos y tener aplicado su corte inicial.");

        var product = await db.Producto.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdProducto == idProducto && !item.EsServicio);
        if (product is null) return await FailAsync(transaction, "Selecciona un artículo físico del catálogo.");

        var sourceBalance = await db.SaldosAlmacen.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdAlmacen == idOrigen && item.IdProducto == idProducto);
        if (sourceBalance is null || sourceBalance.CantidadBase < cantidad)
            return await FailAsync(transaction, $"El almacén «{source.Nombre}» no tiene saldo suficiente para {product.Nombre}.");
        var destinationBalance = await db.SaldosAlmacen.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdAlmacen == idDestino && item.IdProducto == idProducto);
        var destinationPrevious = destinationBalance?.CantidadBase ?? 0m;
        if (destinationPrevious + cantidad > 999_999_999_999_999.999m)
            return await FailAsync(transaction, "La existencia de destino excedería el límite permitido.");

        if (db.Database.IsRelational())
        {
            var taken = await db.SaldosAlmacen
                .Where(item => item.IdAlmacen == idOrigen && item.IdProducto == idProducto
                    && item.CantidadBase >= cantidad && item.RowVersion == sourceBalance.RowVersion)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.CantidadBase, item => item.CantidadBase - cantidad));
            if (taken != 1) return await FailAsync(transaction, "El saldo de origen cambió durante la transferencia. Recarga e inténtalo otra vez.");

            if (destinationBalance is null)
                db.SaldosAlmacen.Add(new StockAlmacen { IdAlmacen = idDestino, IdProducto = idProducto, CantidadBase = cantidad });
            else
            {
                var added = await db.SaldosAlmacen
                    .Where(item => item.IdAlmacen == idDestino && item.IdProducto == idProducto
                        && item.RowVersion == destinationBalance.RowVersion)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.CantidadBase, item => item.CantidadBase + cantidad));
                if (added != 1) return await FailAsync(transaction, "El saldo de destino cambió durante la transferencia. Recarga e inténtalo otra vez.");
            }
        }
        else
        {
            var sourceTracked = await db.SaldosAlmacen.FindAsync(idOrigen, idProducto);
            if (sourceTracked is null || sourceTracked.CantidadBase < cantidad)
                return await FailAsync(transaction, "El saldo de origen cambió durante la transferencia.");
            sourceTracked.CantidadBase -= cantidad;
            var destinationTracked = await db.SaldosAlmacen.FindAsync(idDestino, idProducto);
            if (destinationTracked is null)
                db.SaldosAlmacen.Add(new StockAlmacen { IdAlmacen = idDestino, IdProducto = idProducto, CantidadBase = cantidad });
            else destinationTracked.CantidadBase += cantidad;
        }

        var transfer = new TransferenciaAlmacen
        {
            IdAlmacenOrigen = idOrigen,
            IdAlmacenDestino = idDestino,
            IdProducto = idProducto,
            CantidadBase = cantidad,
            FechaUtc = DateTimeOffset.UtcNow,
            Motivo = motivo.Trim(),
            IdUsuario = usuario
        };
        db.TransferenciasAlmacen.Add(transfer);
        await db.SaveChangesAsync();

        var date = transfer.FechaUtc;
        db.Kardex.AddRange(
            new Kardex
            {
                IdAlmacen = idOrigen, IdProducto = idProducto, FechaUtc = date,
                TipoMovimiento = "Transferencia salida", CantidadBase = cantidad,
                SaldoAnteriorBase = sourceBalance.CantidadBase, SaldoPosteriorBase = sourceBalance.CantidadBase - cantidad,
                TipoReferencia = "TransferenciaAlmacen", IdReferencia = transfer.IdTransferenciaAlmacen,
                IdUsuario = usuario, Observaciones = $"Hacia {destination.Nombre}: {motivo.Trim()}"
            },
            new Kardex
            {
                IdAlmacen = idDestino, IdProducto = idProducto, FechaUtc = date,
                TipoMovimiento = "Transferencia entrada", CantidadBase = cantidad,
                SaldoAnteriorBase = destinationPrevious, SaldoPosteriorBase = destinationPrevious + cantidad,
                TipoReferencia = "TransferenciaAlmacen", IdReferencia = transfer.IdTransferenciaAlmacen,
                IdUsuario = usuario, Observaciones = $"Desde {source.Nombre}: {motivo.Trim()}"
            });
        db.AuditoriaOperacion.Add(AuditoriaFactory.Crear(actor, "TransferenciaAlmacen", transfer.IdTransferenciaAlmacen,
            "Transferencia entre almacenes", null, "Completada",
            $"{cantidad:0.###} de {product.Nombre}; {source.Nombre} → {destination.Nombre}."));
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return new(true, $"Se transfirió {cantidad:0.###} de «{product.Nombre}» de {source.Nombre} a {destination.Nombre}.", transfer.IdTransferenciaAlmacen);
    }

    private static async Task<ResultadoTransferenciaInventario> FailAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        string message)
    {
        if (transaction is not null) await transaction.RollbackAsync();
        return new(false, message);
    }
}
