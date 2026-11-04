using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal.Models;

[Table("Proveedor")]
public partial class Proveedor
{
    [Key]
    [Column("idProveedor")]
    public int IdProveedor { get; set; }

    [Column("nombre")]
    [StringLength(100)]
    public string? Nombre { get; set; }

    [Column("contacto")]
    [StringLength(100)]
    public string? Contacto { get; set; }

    [Column("ruc")]
    [StringLength(11)]
    public string? Ruc { get; set; }

    [Column("direccion")]
    [StringLength(255)]
    public string? Direccion { get; set; }

    [Column("ubigeo", TypeName = "char(6)")]
    [StringLength(6)]
    public string? UbigeoCodigo { get; set; }

    [ForeignKey(nameof(UbigeoCodigo))]
    public Ubigeo? Ubigeo { get; set; }

    [InverseProperty("IdProveedorNavigation")]
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
