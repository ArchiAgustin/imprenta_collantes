using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class TallerConsultaService(ApplicationDbContext db) : ITallerConsultaService
{
    public async Task<IReadOnlyList<Pedido>> ListarAsync() => await db.Pedido.AsNoTracking()
        .Include(item => item.IdVentaNavigation).ThenInclude(sale => sale!.Cliente)
        .Include(item => item.DetallePedidos)
        .OrderBy(item => item.EstadoPedido == EstadoPedido.EnCola ? 0 : 1)
        .ThenBy(item => item.FechaPedido).ThenBy(item => item.IdPedido).ToListAsync();

    public Task<Pedido?> ObtenerAsync(int id) => db.Pedido.AsNoTracking()
        .Include(item => item.IdVentaNavigation).ThenInclude(sale => sale!.Cliente)
        .Include(item => item.DetallePedidos).Include(item => item.IdClienteNavigation)
        .FirstOrDefaultAsync(item => item.IdPedido == id);
}
