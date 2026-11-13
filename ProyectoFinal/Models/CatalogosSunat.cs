using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("mae_tipodocumentopago")]
public sealed class TipoDocumentoPagoSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idtipodocumentopago")]
    public int IdTipoDocumentoPago { get; set; }
    [Required, StringLength(3), Column("codigosunat", TypeName = "char(3)")]
    public string CodigoSunat { get; set; } = string.Empty;
    [Required, StringLength(250), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [StringLength(50), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [Column("mostrar_rc")]
    public bool? MostrarRc { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_moneda")]
public sealed class MonedaSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idmoneda")]
    public int IdMoneda { get; set; }
    [StringLength(1), Column("codigosunat", TypeName = "char(1)")]
    public string? CodigoSunat { get; set; }
    [Required, StringLength(50), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [StringLength(10), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [StringLength(3), Column("simbolo")]
    public string? Simbolo { get; set; }
    [StringLength(3), Column("codigoiso")]
    public string? CodigoIso { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_detraccion")]
public sealed class DetraccionSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("iddetraccion")]
    public int IdDetraccion { get; set; }
    [Required, StringLength(3), Column("codigo", TypeName = "char(3)")]
    public string Codigo { get; set; } = string.Empty;
    [Required, StringLength(200), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [Column("porcentaje", TypeName = "decimal(9,2)")]
    public decimal? Porcentaje { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_tipomediopago")]
public sealed class TipoMedioPagoSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idtipomediopago")]
    public int IdTipoMedioPago { get; set; }
    [StringLength(10), Column("codigosunat")]
    public string? CodigoSunat { get; set; }
    [StringLength(200), Column("descripcion")]
    public string? Descripcion { get; set; }
    [StringLength(30), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [Column("monederodigital")]
    public bool? MonederoDigital { get; set; }
    [Column("cuentainterna")]
    public bool? CuentaInterna { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_motivonc")]
public sealed class MotivoNotaCreditoSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idmotivonc")]
    public int IdMotivoNotaCredito { get; set; }
    [Required, StringLength(2), Column("codigosunat", TypeName = "char(2)")]
    public string CodigoSunat { get; set; } = string.Empty;
    [Required, StringLength(50), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_motivond")]
public sealed class MotivoNotaDebitoSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idmotivond")]
    public int IdMotivoNotaDebito { get; set; }
    [Required, StringLength(2), Column("codigosunat", TypeName = "char(2)")]
    public string CodigoSunat { get; set; } = string.Empty;
    [Required, StringLength(50), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_motivotraslado")]
public sealed class MotivoTrasladoSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idmotivotraslado")]
    public int IdMotivoTraslado { get; set; }
    [Required, StringLength(50), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [StringLength(2), Column("codigosunat", TypeName = "char(2)")]
    public string? CodigoSunat { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_entidadfinanciera_tipo")]
public sealed class TipoEntidadFinancieraSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("identidadfinancieratipo")]
    public int IdTipoEntidadFinanciera { get; set; }
    [Required, StringLength(50), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [StringLength(10), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_entidadfinanciera")]
public sealed class EntidadFinancieraSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("identidadfinanciera")]
    public int IdEntidadFinanciera { get; set; }
    [Column("identidadfinancieratipo")]
    public int IdTipoEntidadFinanciera { get; set; }
    [StringLength(2), Column("codigosunat", TypeName = "char(2)")]
    public string? CodigoSunat { get; set; }
    [Required, StringLength(80), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [StringLength(10), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }

    [ForeignKey(nameof(IdTipoEntidadFinanciera))]
    public TipoEntidadFinancieraSunat? Tipo { get; set; }
}

[Table("mae_tipoactividad")]
public sealed class TipoActividadSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idtipoactividad")]
    public int IdTipoActividad { get; set; }
    [StringLength(10), Column("codigosunat")]
    public string? CodigoSunat { get; set; }
    [StringLength(200), Column("descripcion")]
    public string? Descripcion { get; set; }
    [StringLength(30), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_tipocontribuyente")]
public sealed class TipoContribuyenteSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idtipocontribuyente")]
    public int IdTipoContribuyente { get; set; }
    [StringLength(2), Column("codigosunat", TypeName = "char(2)")]
    public string? CodigoSunat { get; set; }
    [Required, StringLength(150), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_via_tipo")]
public sealed class ViaTipoSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idviatipo")]
    public int IdViaTipo { get; set; }
    [StringLength(2), Column("codigosunat", TypeName = "char(2)")]
    public string? CodigoSunat { get; set; }
    [Required, StringLength(50), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [StringLength(10), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("mae_zona_tipo")]
public sealed class ZonaTipoSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idzonatipo")]
    public int IdZonaTipo { get; set; }
    [StringLength(2), Column("codigosunat", TypeName = "char(2)")]
    public string? CodigoSunat { get; set; }
    [Required, StringLength(50), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [StringLength(10), Column("desc_corta")]
    public string? DescripcionCorta { get; set; }
    [Column("estado")]
    public bool Estado { get; set; }
}

[Table("ven_estadosunat")]
public sealed class EstadoEnvioSunat
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Column("idestadosunat")]
    public int IdEstadoEnvioSunat { get; set; }
    [Required, StringLength(20), Column("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    [Column("estado")]
    public bool Estado { get; set; }
}
