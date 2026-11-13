using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class ProveedoresCatalogoService(ApplicationDbContext db) : IProveedoresCatalogoService
{
    public async Task<PaginaCatalogo<Proveedor>> ListarAsync(string? nombre, string? documento, string? contacto, int pagina)
    {
        const int pageSize = 25;
        var query = db.Proveedor.AsNoTracking().Include(item => item.Ubigeo)
            .Include(item => item.DocumentoIdentidadTipo).AsQueryable();
        if (nombre is not null) query = query.Where(item => item.Nombre != null && item.Nombre.Contains(nombre));
        if (documento is not null) query = query.Where(item =>
            item.NumeroDocumento != null && item.NumeroDocumento.Contains(documento)
            || item.Ruc != null && item.Ruc.Contains(documento));
        if (contacto is not null) query = query.Where(item => item.Contacto != null && item.Contacto.Contains(contacto));
        var total = await query.CountAsync();
        var paginas = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        pagina = Math.Min(Math.Max(1, pagina), paginas);
        var items = await query.OrderBy(item => item.Nombre).ThenBy(item => item.IdProveedor)
            .Skip((pagina - 1) * pageSize).Take(pageSize).ToListAsync();
        return new(items, pagina, paginas, total);
    }

    public Task<Proveedor?> ObtenerAsync(int id) => db.Proveedor.FirstOrDefaultAsync(item => item.IdProveedor == id);
    public Task<Proveedor?> ObtenerDetalleAsync(int id) => db.Proveedor.AsNoTracking()
        .Include(item => item.Ubigeo).Include(item => item.DocumentoIdentidadTipo)
        .Include(item => item.Productos.OrderBy(product => product.Nombre))
        .FirstOrDefaultAsync(item => item.IdProveedor == id);
    public Task<Proveedor?> ObtenerParaEliminarAsync(int id) => db.Proveedor.AsNoTracking()
        .Include(item => item.Ubigeo).FirstOrDefaultAsync(item => item.IdProveedor == id);

    public async Task CrearAsync(Proveedor proveedor)
    {
        db.Proveedor.Add(proveedor);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ActualizarAsync(int id, Proveedor cambios)
    {
        var actual = await db.Proveedor.FindAsync(id);
        if (actual is null) return false;
        actual.Nombre = cambios.Nombre;
        actual.Contacto = cambios.Contacto;
        actual.Ruc = cambios.Ruc;
        actual.IdDocumentoIdentidadTipo = cambios.IdDocumentoIdentidadTipo;
        actual.NumeroDocumento = cambios.NumeroDocumento;
        actual.Direccion = cambios.Direccion;
        actual.UbigeoCodigo = cambios.UbigeoCodigo;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ResultadoEliminarCatalogo> EliminarAsync(int id)
    {
        var proveedor = await db.Proveedor.FindAsync(id);
        if (proveedor is null) return ResultadoEliminarCatalogo.NoEncontrado;
        if (await db.Producto.AnyAsync(item => item.IdProveedor == id)) return ResultadoEliminarCatalogo.EnUso;
        db.Proveedor.Remove(proveedor);
        await db.SaveChangesAsync();
        return ResultadoEliminarCatalogo.Eliminado;
    }

    public Task<bool> ExisteAsync(int id) => db.Proveedor.AnyAsync(item => item.IdProveedor == id);
}
