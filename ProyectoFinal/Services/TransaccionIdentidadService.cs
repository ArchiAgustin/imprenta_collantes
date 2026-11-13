using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProyectoFinal.Data;

namespace ProyectoFinal.Services;

public sealed class TransaccionIdentidadService(ApplicationDbContext db) : ITransaccionIdentidadService
{
    public async Task<ITransaccionIdentidad?> IniciarSerializableAsync() => db.Database.IsRelational()
        ? new Handler(await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)) : null;

    private sealed class Handler(IDbContextTransaction transaction) : ITransaccionIdentidad
    {
        public Task CommitAsync() => transaction.CommitAsync();
        public Task RollbackAsync() => transaction.RollbackAsync();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
