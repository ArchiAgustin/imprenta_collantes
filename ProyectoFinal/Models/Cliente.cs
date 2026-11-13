using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal.Models;

[Table("Cliente")]
public partial class Cliente
{
    [Key]
    [Column("idCliente")]
    public int IdCliente { get; set; }

    [Column("nombre")]
    [StringLength(100)]
    public string? Nombre { get; set; }

    [Column("correo")]
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido.")]
    public string? Correo { get; set; }

    [Column("telefono")]
    [StringLength(20)]
    [Phone(ErrorMessage = "Ingresa un teléfono válido.")]
    public string? Telefono { get; set; }

    [Column("direccion")]
    [StringLength(255)]
    public string? Direccion { get; set; }

    [Column("dni")]
    [StringLength(20)]
    public string? Dni { get; set; }

    [Column("idDocumentoIdentidadTipo")]
    public int? IdDocumentoIdentidadTipo { get; set; }

    [Column("numeroDocumento")]
    [StringLength(11)]
    public string? NumeroDocumento { get; set; }

    [Column("ubigeo", TypeName = "char(6)")]
    [StringLength(6)]
    public string? UbigeoCodigo { get; set; }

    [ForeignKey(nameof(IdDocumentoIdentidadTipo))]
    public DocumentoIdentidadTipo? DocumentoIdentidadTipo { get; set; }

    [ForeignKey(nameof(UbigeoCodigo))]
    public Ubigeo? Ubigeo { get; set; }

    [InverseProperty("IdClienteNavigation")]
    public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();

    [InverseProperty("IdClienteNavigation")]
    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
