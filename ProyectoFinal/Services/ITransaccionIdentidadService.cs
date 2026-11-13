namespace ProyectoFinal.Services;

public interface ITransaccionIdentidad : IAsyncDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
}

public interface ITransaccionIdentidadService
{
    Task<ITransaccionIdentidad?> IniciarSerializableAsync();
}
