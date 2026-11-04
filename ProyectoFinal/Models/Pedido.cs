using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ProyectoFinal.Models;

[Table("Pedido")]
public partial class Pedido
{
    [Key]
    [Column("idPedido")]
    public int IdPedido { get; set; }

    [Column("idCliente")]
    public int? IdCliente { get; set; }

    [Column("idUsuario")]
    [StringLength(450)]
    public string? IdUsuario { get; set; }

    [Column("fechaPedido")]
    public DateOnly? FechaPedido { get; set; }

    [Column("estadoPedido")]
    [StringLength(50)]
    public string? EstadoPedido { get; set; } = ProyectoFinal.Models.EstadoPedido.EnCola;

    [Column("total", TypeName = "decimal(10, 2)")]
    public decimal? Total { get; set; }

    [Column("idCotizacion")]
    public int? IdCotizacion { get; set; }

    [Column("idVenta")]
    public int? IdVenta { get; set; }

    [InverseProperty("IdPedidoNavigation")]
    public virtual ICollection<DetallePedido> DetallePedidos { get; set; } = new List<DetallePedido>();

    [ForeignKey("IdCliente")]
    [InverseProperty("Pedidos")]
    public virtual Cliente? IdClienteNavigation { get; set; }

    [ForeignKey("IdCotizacion")]
    [InverseProperty("Pedidos")]
    public virtual Cotizacion? IdCotizacionNavigation { get; set; }

    [ForeignKey("IdVenta")]
    public virtual Venta? IdVentaNavigation { get; set; }

    [ForeignKey("IdUsuario")]
    public virtual IdentityUser? IdUsuarioNavigation { get; set; }
}
