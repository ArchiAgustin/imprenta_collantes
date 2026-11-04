using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal.Models;

[Table("DetalleCotizacion")]
public partial class DetalleCotizacion
{
    [Key]
    [Column("idDetalleCotizacion")]
    public int IdDetalleCotizacion { get; set; }

    [Column("idCotizacion")]
    public int? IdCotizacion { get; set; }

    [Column("idProducto")]
    public int? IdProducto { get; set; }

    [Column("cantidad")]
    public int? Cantidad { get; set; }

    [Column("precioUnitario", TypeName = "decimal(10, 2)")]
    public decimal? PrecioUnitario { get; set; }

    [Column("productoNombreEmision"), StringLength(100)]
    public string? ProductoNombreEmision { get; set; }

    [ForeignKey("IdCotizacion")]
    [InverseProperty("DetalleCotizacions")]
    public virtual Cotizacion? IdCotizacionNavigation { get; set; }

    [ForeignKey("IdProducto")]
    [InverseProperty("DetalleCotizacions")]
    public virtual Producto? IdProductoNavigation { get; set; }
}
