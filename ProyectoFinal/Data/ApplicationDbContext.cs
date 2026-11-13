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
        public DbSet<TransferenciaAlmacen> TransferenciasAlmacen { get; set; } = default!;
        public DbSet<CorteInventario> CortesInventario { get; set; } = default!;
        public DbSet<CorteInventarioDetalle> DetallesCorteInventario { get; set; } = default!;
        public DbSet<SerieInventario> SeriesInventario { get; set; } = default!;
        public DbSet<Inventario> Inventarios { get; set; } = default!;
        public DbSet<InventarioDetalle> DetallesInventario { get; set; } = default!;
        public DbSet<Kardex> Kardex { get; set; } = default!;
        public DbSet<SerieDocumentoInterno> SeriesDocumentoInterno { get; set; } = default!;
        public DbSet<TipoDocumentoPagoSunat> TiposDocumentoPagoSunat { get; set; } = default!;
        public DbSet<MonedaSunat> MonedasSunat { get; set; } = default!;
        public DbSet<DetraccionSunat> DetraccionesSunat { get; set; } = default!;
        public DbSet<TipoMedioPagoSunat> TiposMedioPagoSunat { get; set; } = default!;
        public DbSet<MotivoNotaCreditoSunat> MotivosNotaCreditoSunat { get; set; } = default!;
        public DbSet<MotivoNotaDebitoSunat> MotivosNotaDebitoSunat { get; set; } = default!;
        public DbSet<MotivoTrasladoSunat> MotivosTrasladoSunat { get; set; } = default!;
        public DbSet<TipoEntidadFinancieraSunat> TiposEntidadFinancieraSunat { get; set; } = default!;
        public DbSet<EntidadFinancieraSunat> EntidadesFinancierasSunat { get; set; } = default!;
        public DbSet<TipoActividadSunat> TiposActividadSunat { get; set; } = default!;
        public DbSet<TipoContribuyenteSunat> TiposContribuyenteSunat { get; set; } = default!;
        public DbSet<ViaTipoSunat> TiposViaSunat { get; set; } = default!;
        public DbSet<ZonaTipoSunat> TiposZonaSunat { get; set; } = default!;
        public DbSet<EstadoEnvioSunat> EstadosEnvioSunat { get; set; } = default!;
        public DbSet<ConfiguracionCorreo> ConfiguracionCorreo { get; set; } = default!;
        public DbSet<TramoDescuentoProducto> TramosDescuentoProducto { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Producto>().Property(producto => producto.EsServicio).HasDefaultValue(false);
            modelBuilder.Entity<TramoDescuentoProducto>().HasOne(item => item.Producto).WithMany()
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TramoDescuentoProducto>().HasIndex(item => new { item.IdProducto, item.CantidadMinima }).IsUnique();
            modelBuilder.Entity<TramoDescuentoProducto>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_TramoDescuento_Cantidad", "[CantidadMinima] BETWEEN 1 AND 1000000");
                table.HasCheckConstraint("CK_TramoDescuento_Porcentaje", "[DescuentoPorcentaje] BETWEEN 0 AND 99.99");
            });
            // SQL Server rejects EF's OUTPUT clause on a table with a trigger.
            modelBuilder.Entity<Producto>().ToTable(table => table.UseSqlOutputClause(false));
            modelBuilder.Entity<Cotizacion>().Property(cotizacion => cotizacion.Estado).HasDefaultValue("Emitida");
            modelBuilder.Entity<MenuSistema>().HasOne(menu => menu.MenuPadre).WithMany(menu => menu.Hijos)
                .HasForeignKey(menu => menu.IdMenuPadre).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MenuSistema>().HasIndex(menu => menu.IdMenuPadre);
            modelBuilder.Entity<MenuSistema>().HasIndex(menu => menu.Destino).IsUnique()
                .HasFilter("[destino] IS NOT NULL");
            modelBuilder.Entity<EntidadFinancieraSunat>()
                .HasOne(item => item.Tipo).WithMany().HasForeignKey(item => item.IdTipoEntidadFinanciera)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EntidadFinancieraSunat>().HasIndex(item => item.IdTipoEntidadFinanciera);
            modelBuilder.Entity<SerieDocumentoInterno>().HasIndex(item => new { item.TipoDocumento, item.Serie }).IsUnique();
            modelBuilder.Entity<SerieDocumentoInterno>().HasIndex(item => item.TipoDocumento).IsUnique()
                .HasFilter("[activa] = 1");
            modelBuilder.Entity<SerieDocumentoInterno>().HasOne(item => item.TipoComprobanteSunat).WithMany()
                .HasForeignKey(item => item.IdTipoComprobanteSunat).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SerieDocumentoInterno>().HasIndex(item => item.IdTipoComprobanteSunat);
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
            modelBuilder.Entity<Proveedor>().HasOne(item => item.DocumentoIdentidadTipo).WithMany().HasForeignKey(item => item.IdDocumentoIdentidadTipo).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Proveedor>().HasIndex(item => new { item.IdDocumentoIdentidadTipo, item.NumeroDocumento })
                .IsUnique().HasFilter("[idDocumentoIdentidadTipo] IS NOT NULL AND [numeroDocumento] IS NOT NULL");

            modelBuilder.Entity<Producto>().HasOne(item => item.UnidadMedidaBase).WithMany()
                .HasForeignKey(item => item.IdUnidadMedidaBase).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PresentacionProducto>().HasOne(item => item.Producto).WithMany(item => item.Presentaciones)
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PresentacionProducto>().HasOne(item => item.UnidadMedida).WithMany()
                .HasForeignKey(item => item.IdUnidadMedida).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PresentacionProducto>().ToTable(table => table.HasCheckConstraint("CK_PresentacionProducto_Factor", "[factor] > 0"));
            modelBuilder.Entity<DetalleCotizacion>().HasOne(item => item.Presentacion).WithMany()
                .HasForeignKey(item => item.IdPresentacionProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DetalleVenta>().HasOne(item => item.Presentacion).WithMany()
                .HasForeignKey(item => item.IdPresentacionProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DetallePedido>().HasOne(item => item.Presentacion).WithMany()
                .HasForeignKey(item => item.IdPresentacionProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Almacen>().HasIndex(item => item.Nombre).IsUnique();
            modelBuilder.Entity<Almacen>().HasIndex(item => item.EsPredeterminado).IsUnique()
                .HasFilter("[predeterminado] = 1");
            modelBuilder.Entity<Venta>().HasOne(item => item.Almacen).WithMany()
                .HasForeignKey(item => item.IdAlmacen).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Venta>().HasIndex(item => item.IdAlmacen);

            modelBuilder.Entity<TransferenciaAlmacen>().HasOne(item => item.AlmacenOrigen).WithMany()
                .HasForeignKey(item => item.IdAlmacenOrigen).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransferenciaAlmacen>().HasOne(item => item.AlmacenDestino).WithMany()
                .HasForeignKey(item => item.IdAlmacenDestino).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransferenciaAlmacen>().HasOne(item => item.Producto).WithMany()
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TransferenciaAlmacen>().HasIndex(item => new { item.IdAlmacenOrigen, item.FechaUtc });
            modelBuilder.Entity<TransferenciaAlmacen>().HasIndex(item => new { item.IdAlmacenDestino, item.FechaUtc });
            modelBuilder.Entity<TransferenciaAlmacen>().ToTable(table => table.HasCheckConstraint(
                "CK_TransferenciaAlmacen_Almacenes", "[idAlmacenOrigen] <> [idAlmacenDestino]"));

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

            modelBuilder.Entity<SerieInventario>().HasIndex(item => item.Serie).IsUnique();
            modelBuilder.Entity<SerieInventario>().HasIndex(item => item.Activa).IsUnique()
                .HasFilter("[activa] = 1");
            modelBuilder.Entity<SerieInventario>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_SerieInventario_Correlativo", "[siguienteCorrelativo] BETWEEN 1 AND 99999999");
                table.HasCheckConstraint("CK_SerieInventario_Digitos", "[digitos] = 8");
            });

            modelBuilder.Entity<Inventario>().HasIndex(item => new { item.Serie, item.Correlativo }).IsUnique();
            modelBuilder.Entity<Inventario>().HasIndex(item => new { item.IdAlmacen, item.Fecha });
            modelBuilder.Entity<Inventario>().HasIndex(item => item.IdCorteInventarioOrigen).IsUnique()
                .HasFilter("[idCorteInventarioOrigen] IS NOT NULL");
            modelBuilder.Entity<Inventario>().ToTable(table =>
                table.HasCheckConstraint("CK_Inventario_Correlativo", "[correlativo] BETWEEN 1 AND 99999999"));
            modelBuilder.Entity<Inventario>().HasOne(item => item.Almacen).WithMany()
                .HasForeignKey(item => item.IdAlmacen).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Inventario>().HasOne(item => item.CorteInventarioOrigen).WithMany()
                .HasForeignKey(item => item.IdCorteInventarioOrigen).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventarioDetalle>().HasIndex(item => new { item.IdInventario, item.IdProducto }).IsUnique();
            modelBuilder.Entity<InventarioDetalle>().HasOne(item => item.Inventario).WithMany(item => item.Detalles)
                .HasForeignKey(item => item.IdInventario).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventarioDetalle>().HasOne(item => item.Producto).WithMany()
                .HasForeignKey(item => item.IdProducto).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InventarioDetalle>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_InventarioDetalle_Cantidad", "[cantidadContadaBase] >= 0");
                table.HasCheckConstraint("CK_InventarioDetalle_Sistema", "[cantidadSistemaBase] IS NULL OR [cantidadSistemaBase] >= 0");
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
