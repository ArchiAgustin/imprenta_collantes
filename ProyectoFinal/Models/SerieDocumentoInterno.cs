using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models;

[Table("SerieDocumentoInterno")]
public sealed class SerieDocumentoInterno
{
    [Key, Column("idSerieDocumentoInterno")]
    public int IdSerieDocumentoInterno { get; set; }

    [Required, StringLength(20), Column("tipoDocumento")]
    public string TipoDocumento { get; set; } = string.Empty;

    [Required, StringLength(8), RegularExpression("^[A-Z0-9]{1,8}$"), Column("serie")]
    public string Serie { get; set; } = string.Empty;

    [Range(1, int.MaxValue), Column("siguienteCorrelativo")]
    public int SiguienteCorrelativo { get; set; }

    [Range(4, 10), Column("digitos")]
    public int Digitos { get; set; } = 6;

    // Optional for quotations; an internal sale can be classified by a SUNAT
    // reference type without becoming an electronic or tax-issued document.
    [Column("idTipoComprobanteSunat")]
    public int? IdTipoComprobanteSunat { get; set; }

    [ForeignKey(nameof(IdTipoComprobanteSunat))]
    public TipoDocumentoPagoSunat? TipoComprobanteSunat { get; set; }

    [Column("activa")]
    public bool Activa { get; set; }
}
