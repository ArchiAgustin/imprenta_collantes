using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Models;

namespace ProyectoFinal.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<ProyectoFinal.Models.Cliente> Cliente { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.Proveedor> Proveedor { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.Producto> Producto { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.Cotizacion> Cotizacion { get; set; } = default!;
    }
}
