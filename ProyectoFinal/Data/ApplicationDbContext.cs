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
        public DbSet<ProyectoFinal.Models.DetalleCotizacion> DetalleCotizacion { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.Venta> Venta { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.DetalleVenta> DetalleVenta { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.Pedido> Pedido { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.DetallePedido> DetallePedido { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.AuditoriaOperacion> AuditoriaOperacion { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.MenuSistema> MenuSistema { get; set; } = default!;
        public DbSet<ProyectoFinal.Models.PermisoRolMenu> PermisoRolMenu { get; set; } = default!;
        public DbSet<Ubigeo> Ubigeos { get; set; } = default!;
        public DbSet<DocumentoIdentidadTipo> DocumentoIdentidadTipos { get; set; } = default!;
        public DbSet<UnidadMedida> UnidadesMedida { get; set; } = default!;
        public DbSet<PresentacionProducto> PresentacionesProducto { get; set; } = default!;
        public DbSet<Almacen> Almacenes { get; set; } = default!;
        public DbSet<StockAlmacen> SaldosAlmacen { get; set; } = default!;
        public DbSet<CorteInventario> CortesInventario { get; set; } = default!;
        public DbSet<CorteInventarioDetalle> DetallesCorteInventario { get; set; } = default!;
        public DbSet<Kardex> Kardex { get; set; } = default!;
        public DbSet<SerieDocumentoInterno> SeriesDocumentoInterno { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Producto>().Property(producto => producto.EsServicio).HasDefaultValue(false);
            // SQL Server rejects EF's OUTPUT clause on a table with a trigger.
            modelBuilder.Entity<Producto>().ToTable(table => table.UseSqlOutputClause(false));
            modelBuilder.Entity<Cotizacion>().Property(cotizacion => cotizacion.Estado).HasDefaultValue("Emitida");
            modelBuilder.Entity<SerieDocumentoInterno>().HasIndex(item => new { item.TipoDocumento, item.Serie }).IsUnique();
            modelBuilder.Entity<SerieDocumentoInterno>().HasIndex(item => item.TipoDocumento).IsUnique()
                .HasFilter("[activa] = 1");
            modelBuilder.Entity<SerieDocumentoInterno>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_SerieInterna_Siguiente", "[siguienteCorrelativo] > 0");
                table.HasCheckConstraint("CK_SerieInterna_Digitos", "[digitos] BETWEEN 4 AND 10");
                table.HasCheckConstraint("CK_SerieInterna_Tipo", "[tipoDocumento] IN (N'Cotizacion', N'Venta')");
            });
            modelBuilder.Entity<Cotizacion>().HasIndex(item => new { item.SerieInterna, item.CorrelativoInterno })
                .IsUnique().HasFilter("[serieInterna] IS NOT NULL AND [correlativoInterno] IS NOT NULL");
            modelBuilder.Entity<Venta>().HasIndex(item => new { item.SerieInterna, item.CorrelativoInterno })
                .IsUnique().HasFilter("[serieInterna] IS NOT NULL AND [correlativoInterno] IS NOT NULL");
            modelBuilder.Entity<Cotizacion>().HasOne<Cotizacion>().WithMany()
                .HasForeignKey(item => item.IdCotizacionOrigen).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Pedido>().HasIndex(pedido => pedido.IdVenta).IsUnique();
            modelBuilder.Entity<Venta>().HasIndex(venta => venta.IdCotizacion).IsUnique();
            modelBuilder.Entity<Venta>()
                .HasOne(venta => venta.Cotizacion)
                .WithOne(cotizacion => cotizacion.Venta)
                .HasForeignKey<Venta>(venta => venta.IdCotizacion)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Venta>()
                .HasOne(venta => venta.Pedido)
                .WithOne(pedido => pedido.IdVentaNavigation)
                .HasForeignKey<Pedido>(pedido => pedido.IdVenta)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DetalleVenta>()
                .HasOne(line => line.Producto)
                .WithMany(product => product.DetalleVentas)
                .HasForeignKey(line => line.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DetallePedido>()
                .HasOne(line => line.IdProductoNavigation)
                .WithMany(product => product.DetallePedidos)
                .HasForeignKey(line => line.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PermisoRolMenu>().HasIndex(permission => new { permission.RoleId, permission.MenuSistemaId }).IsUnique();
            modelBuilder.Entity<PermisoRolMenu>().HasKey(permission => permission.IdPermiso);
            modelBuilder.Entity<PermisoRolMenu>().Property(permission => permission.IdPermiso).ValueGeneratedOnAdd();
            modelBuilder.Entity<MenuSistema>().HasIndex(menu => menu.Codigo).IsUnique();
            modelBuilder.Entity<AuditoriaOperacion>().HasIndex(entry => new { entry.TipoEntidad, entry.IdEntidad, entry.FechaUtc });
            modelBuilder.Entity<Ubigeo>().HasIndex(item => item.Codigo).IsUnique();
            modelBuilder.Entity<Ubigeo>().HasIndex(item => item.Parent);
            modelBuilder.Entity<DocumentoIdentidadTipo>().HasIndex(item => item.Codigo).IsUnique();
            modelBuilder.Entity<Cliente>().HasOne(item => item.Ubigeo).WithMany().HasForeignKey(item => item.UbigeoCodigo).HasPrincipalKey(item => item.Codigo).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Proveedor>().HasOne(item => item.Ubigeo).WithMany().HasForeignKey(item => item.UbigeoCodigo).HasPrincipalKey(item => item.Codigo).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Cliente>().HasOne(item => item.DocumentoIdentidadTipo).WithMany().HasForeignKey(item => item.IdDocumentoIdentidadTipo).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Cliente>().HasIndex(item => new { item.IdDocumentoIdentidadTipo, item.NumeroDocumento })
                .IsUnique().HasFilter("[idDocumentoIdentidadTipo] IS NOT NULL AND [numeroDocumento] IS NOT NULL");
            modelBuilder.Entity<Proveedor>().HasIndex(item => item.Ruc).IsUnique().HasFilter("[ruc] IS NOT NULL");

            modelBuilder.Entity<Producto>().HasOne(item => item.UnidadMedidaBase).WithMany()
                .HasForeignKey(item => item.IdUnidadMedidaBase).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PresentacionProducto>().HasOne(item => item.Producto).WithMany()
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PresentacionProducto>().HasOne(item => item.UnidadMedida).WithMany()
                .HasForeignKey(item => item.IdUnidadMedida).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PresentacionProducto>().ToTable(table => table.HasCheckConstraint("CK_PresentacionProducto_Factor", "[factor] > 0"));
            modelBuilder.Entity<Almacen>().HasIndex(item => item.Nombre).IsUnique();

            modelBuilder.Entity<StockAlmacen>().HasKey(item => new { item.IdAlmacen, item.IdProducto });
            modelBuilder.Entity<StockAlmacen>().HasOne(item => item.Almacen).WithMany()
                .HasForeignKey(item => item.IdAlmacen).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockAlmacen>().HasOne(item => item.Producto).WithMany()
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<StockAlmacen>().ToTable(table => table.HasCheckConstraint("CK_Stock_CantidadBase", "[cantidadBase] >= 0"));

            modelBuilder.Entity<CorteInventario>().HasOne(item => item.Almacen).WithMany()
                .HasForeignKey(item => item.IdAlmacen).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CorteInventarioDetalle>().HasOne(item => item.Corte).WithMany(item => item.Detalles)
                .HasForeignKey(item => item.IdCorteInventario).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CorteInventarioDetalle>().HasOne(item => item.Producto).WithMany()
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CorteInventarioDetalle>().HasIndex(item => new { item.IdCorteInventario, item.IdProducto }).IsUnique();
            modelBuilder.Entity<CorteInventarioDetalle>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_CorteDetalle_Contada", "[cantidadContadaBase] >= 0");
                table.HasCheckConstraint("CK_CorteDetalle_Sistema", "[cantidadSistemaBase] IS NULL OR [cantidadSistemaBase] >= 0");
            });

            modelBuilder.Entity<Kardex>().HasOne(item => item.Almacen).WithMany()
                .HasForeignKey(item => item.IdAlmacen).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Kardex>().HasOne(item => item.Producto).WithMany()
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Kardex>().HasIndex(item => new { item.IdAlmacen, item.IdProducto, item.FechaUtc });
            modelBuilder.Entity<Kardex>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_Kardex_Cantidad", "[cantidadBase] > 0");
                table.HasCheckConstraint("CK_Kardex_Saldos", "[saldoAnteriorBase] >= 0 AND [saldoPosteriorBase] >= 0");
            });
        }
    }
}
