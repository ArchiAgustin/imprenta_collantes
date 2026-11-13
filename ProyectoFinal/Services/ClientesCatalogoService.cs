using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class ClientesCatalogoService(ApplicationDbContext db) : IClientesCatalogoService
{
    public async Task<PaginaCatalogo<Cliente>> ListarAsync(string? nombre, string? documento, string? correo, string? telefono, int pagina)
    {
        const int pageSize = 25;
        var query = db.Cliente.AsNoTracking().Include(item => item.DocumentoIdentidadTipo)
            .Include(item => item.Ubigeo).AsQueryable();
        if (nombre is not null) query = query.Where(item => item.Nombre != null && item.Nombre.Contains(nombre));
        if (documento is not null) query = query.Where(item =>
            item.NumeroDocumento != null && item.NumeroDocumento.Contains(documento)
            || item.Dni != null && item.Dni.Contains(documento));
        if (correo is not null) query = query.Where(item => item.Correo != null && item.Correo.Contains(correo));
        if (telefono is not null) query = query.Where(item => item.Telefono != null && item.Telefono.Contains(telefono));
        var total = await query.CountAsync();
        var paginas = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        pagina = Math.Min(Math.Max(1, pagina), paginas);
        var items = await query.OrderBy(item => item.Nombre).ThenBy(item => item.IdCliente)
            .Skip((pagina - 1) * pageSize).Take(pageSize).ToListAsync();
        return new(items, pagina, paginas, total);
    }

    public Task<Cliente?> ObtenerAsync(int id) => db.Cliente.FirstOrDefaultAsync(item => item.IdCliente == id);
    public Task<Cliente?> ObtenerParaEliminarAsync(int id) => db.Cliente.AsNoTracking()
        .Include(item => item.DocumentoIdentidadTipo).Include(item => item.Ubigeo)
        .FirstOrDefaultAsync(item => item.IdCliente == id);

    public async Task CrearAsync(Cliente cliente, bool esDni)
    {
        if (esDni) cliente.Dni = cliente.NumeroDocumento;
        db.Cliente.Add(cliente);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ActualizarAsync(int id, Cliente cambios, bool esDni)
    {
        var actual = await db.Cliente.FindAsync(id);
        if (actual is null) return false;
        actual.Nombre = cambios.Nombre;
        actual.Correo = cambios.Correo;
        actual.Telefono = cambios.Telefono;
        actual.Direccion = cambios.Direccion;
        actual.IdDocumentoIdentidadTipo = cambios.IdDocumentoIdentidadTipo;
        actual.NumeroDocumento = cambios.NumeroDocumento;
        actual.UbigeoCodigo = cambios.UbigeoCodigo;
        actual.Dni = esDni ? cambios.NumeroDocumento : null;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ResultadoEliminarCatalogo> EliminarAsync(int id)
    {
        var cliente = await db.Cliente.FindAsync(id);
        if (cliente is null) return ResultadoEliminarCatalogo.NoEncontrado;
        if (await db.Cotizacion.AnyAsync(item => item.IdCliente == id)
            || await db.Pedido.AnyAsync(item => item.IdCliente == id))
            return ResultadoEliminarCatalogo.EnUso;
        db.Cliente.Remove(cliente);
        await db.SaveChangesAsync();
        return ResultadoEliminarCatalogo.Eliminado;
    }

    public Task<bool> ExisteAsync(int id) => db.Cliente.AnyAsync(item => item.IdCliente == id);
}
