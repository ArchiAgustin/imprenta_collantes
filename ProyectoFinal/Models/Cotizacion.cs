using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ProyectoFinal.Models;

[Table("Cotizacion")]
public partial class Cotizacion
{
    [Key]
    [Column("idCotizacion")]
    public int IdCotizacion { get; set; }

    [Column("idCliente")]
    public int? IdCliente { get; set; }

    [Column("idUsuario")]
    [StringLength(450)]
    public string? IdUsuario { get; set; }

    [Column("fechaCreacion")]
    public DateOnly? FechaCreacion { get; set; }

    [Column("fechaVencimiento")]
    public DateOnly? FechaVencimiento { get; set; }

    [Column("detalleCotizacion")]
    public string? DetalleCotizacion { get; set; }

    [Column("total", TypeName = "decimal(10, 2)")]
    public decimal? Total { get; set; }

    [InverseProperty("IdCotizacionNavigation")]
    public virtual ICollection<DetalleCotizacion> DetalleCotizacions { get; set; } = new List<DetalleCotizacion>();

    [ForeignKey("IdCliente")]
    [InverseProperty("Cotizacions")]
    public virtual Cliente? IdClienteNavigation { get; set; }

    [ForeignKey("IdUsuario")]
    public virtual IdentityUser? IdUsuarioNavigation { get; set; }

    [InverseProperty("IdCotizacionNavigation")]
    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
