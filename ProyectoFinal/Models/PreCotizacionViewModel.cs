using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ProyectoFinal.Models;

public sealed class PreCotizacionViewModel
{
    [StringLength(4000)]
    public string? Solicitud { get; set; }

    [StringLength(4000)]
    public string? TextoRevisado { get; set; }

    public bool TextoAprobado { get; set; }
    public bool Preparada { get; set; }
    public bool Analizada { get; set; }
    public string? Aviso { get; set; }
    public string? FechaRequerida { get; set; }
    public string? Observaciones { get; set; }
    public string? Descripcion { get; set; }
    public int? ClienteId { get; set; }
    [StringLength(100)]
    public string? ClienteNombreSugerido { get; set; }
    [StringLength(5)]
    public string? ClienteDocumentoTipoSugerido { get; set; }
    [StringLength(20)]
    public string? ClienteDocumentoNumeroSugerido { get; set; }
    public bool ConfirmarIdentidadLocal { get; set; }
    [BindNever]
    public int? ClienteIdSugerido { get; set; }
    public List<PreCotizacionLineaViewModel> Lineas { get; set; } = new();
    public List<Cliente> Clientes { get; set; } = new();
    public List<Producto> Productos { get; set; } = new();
}

public sealed class PreCotizacionLineaViewModel
{
    public string NombreSugerido { get; set; } = string.Empty;
    public string Especificacion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int? ProductoId { get; set; }
    public int? IdPresentacionProducto { get; set; }
    [BindNever]
    public List<PreCotizacionProductoCandidato> Candidatos { get; set; } = new();
}

public sealed record PreCotizacionProductoCandidato(int IdProducto, string Nombre, string Tipo, string Motivo);

public sealed record SolicitudExtraida(IReadOnlyList<LineaExtraida> Lineas, string Observaciones, string FechaRequerida);
public sealed record LineaExtraida(string Nombre, int Cantidad, string Especificacion);
public sealed record ResultadoExtraccion(bool Exito, string Mensaje, SolicitudExtraida? Datos = null);
