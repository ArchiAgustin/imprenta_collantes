using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ProyectoFinal.Models;

[Table("Venta")]
public class Venta
{
    [Key]
    [Column("idVenta")]
    public int IdVenta { get; set; }

    [Column("serieInterna"), StringLength(8)]
    public string? SerieInterna { get; set; }

    [Column("correlativoInterno")]
    public int? CorrelativoInterno { get; set; }

    [Column("digitosNumeroInterno")]
    public int? DigitosNumeroInterno { get; set; }

    [NotMapped]
    public string NumeroInterno => SerieInterna is { Length: > 0 } && CorrelativoInterno is { } numero
        ? $"{SerieInterna}-{numero.ToString("D" + (DigitosNumeroInterno is >= 4 and <= 10 ? DigitosNumeroInterno.Value : 6))}" : $"Histórica #{IdVenta}";

    // Commercial snapshot, not a SUNAT document. Legacy rows remain null.
    [Column("clienteNombreEmision"), StringLength(100)]
    public string? ClienteNombreEmision { get; set; }

    [Column("clienteDocumentoEmision"), StringLength(20)]
    public string? ClienteDocumentoEmision { get; set; }

    [Column("clienteDireccionEmision"), StringLength(255)]
    public string? ClienteDireccionEmision { get; set; }

    [Column("subtotalSinIgvEmision", TypeName = "decimal(10,2)")]
    public decimal? SubtotalSinIgvEmision { get; set; }

    [Column("igvEmision", TypeName = "decimal(10,2)")]
    public decimal? IgvEmision { get; set; }

    [Column("idCotizacion")]
    public int IdCotizacion { get; set; }

    [Column("idCliente")]
    public int IdCliente { get; set; }

    [Column("idUsuario")]
    [StringLength(450)]
    public string? IdUsuario { get; set; }

    [Column("fechaVenta")]
    public DateTimeOffset FechaVentaUtc { get; set; }

    [Column("estado")]
    [StringLength(30)]
    public string Estado { get; set; } = EstadoVenta.Confirmada;

    [Column("total", TypeName = "decimal(10, 2)")]
    public decimal Total { get; set; }

    [ForeignKey(nameof(IdCotizacion))]
    public virtual Cotizacion? Cotizacion { get; set; }

    [ForeignKey(nameof(IdCliente))]
    public virtual Cliente? Cliente { get; set; }

    [ForeignKey(nameof(IdUsuario))]
    public virtual IdentityUser? Usuario { get; set; }

    public virtual Pedido? Pedido { get; set; }

    public virtual ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
}

public static class EstadoVenta
{
    public const string Confirmada = "Confirmada";
    public const string Cancelada = "Cancelada";
}
