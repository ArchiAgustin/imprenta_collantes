using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal.Models;

[Table("DetallePedido")]
public partial class DetallePedido
{
    [Key]
    [Column("idDetallePedido")]
    public int IdDetallePedido { get; set; }

    [Column("idPedido")]
    public int? IdPedido { get; set; }

    [Column("idProducto")]
    public int? IdProducto { get; set; }

    [Column("cantidad")]
    public int? Cantidad { get; set; }

    [Column("precioUnitario", TypeName = "decimal(10, 2)")]
    public decimal? PrecioUnitario { get; set; }

    [Column("productoNombre")]
    [StringLength(100)]
    public string? ProductoNombre { get; set; }

    [Column("esServicio")]
    public bool EsServicio { get; set; }

    [Column("idPresentacionProducto")]
    public int? IdPresentacionProducto { get; set; }

    [Column("factorPresentacion", TypeName = "decimal(12,3)")]
    public decimal FactorPresentacion { get; set; } = 1m;

    [Column("presentacionDescripcion"), StringLength(100)]
    public string? PresentacionDescripcion { get; set; }

    [Column("unidadBaseDescripcion"), StringLength(100)]
    public string? UnidadBaseDescripcion { get; set; }

    [ForeignKey("IdPedido")]
    [InverseProperty("DetallePedidos")]
    public virtual Pedido? IdPedidoNavigation { get; set; }

    [ForeignKey("IdProducto")]
    [InverseProperty("DetallePedidos")]
    public virtual Producto? IdProductoNavigation { get; set; }

    [ForeignKey(nameof(IdPresentacionProducto))]
    public PresentacionProducto? Presentacion { get; set; }
}
