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

    [Column("serieInterna"), StringLength(8)]
    public string? SerieInterna { get; set; }

    [Column("correlativoInterno")]
    public int? CorrelativoInterno { get; set; }

    [Column("digitosNumeroInterno")]
    public int? DigitosNumeroInterno { get; set; }

    [Column("idCotizacionOrigen")]
    public int? IdCotizacionOrigen { get; set; }

    [Column("clienteNombreEmision"), StringLength(100)]
    public string? ClienteNombreEmision { get; set; }

    [Column("clienteDocumentoEmision"), StringLength(20)]
    public string? ClienteDocumentoEmision { get; set; }

    [Column("clienteDireccionEmision"), StringLength(255)]
    public string? ClienteDireccionEmision { get; set; }

    [NotMapped]
    public string NumeroInterno => SerieInterna is { Length: > 0 } && CorrelativoInterno is { } numero
        ? $"{SerieInterna}-{numero.ToString("D" + (DigitosNumeroInterno is >= 4 and <= 10 ? DigitosNumeroInterno.Value : 6))}" : $"Histórica #{IdCotizacion}";

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

    [Column("estado", TypeName = "nvarchar(30)")]
    [StringLength(30)]
    public string Estado { get; set; } = "Emitida";

    [InverseProperty("IdCotizacionNavigation")]
    public virtual ICollection<DetalleCotizacion> DetalleCotizacions { get; set; } = new List<DetalleCotizacion>();

    [ForeignKey("IdCliente")]
    [InverseProperty("Cotizacions")]
    public virtual Cliente? IdClienteNavigation { get; set; }

    [ForeignKey("IdUsuario")]
    public virtual IdentityUser? IdUsuarioNavigation { get; set; }

    [InverseProperty("IdCotizacionNavigation")]
    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();

    public virtual Venta? Venta { get; set; }
}
