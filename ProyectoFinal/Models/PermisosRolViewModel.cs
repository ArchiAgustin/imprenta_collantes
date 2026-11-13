using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models;

public sealed class PermisosRolViewModel
{
    [Required]
    public string RolId { get; set; } = string.Empty;

    public string RolNombre { get; set; } = string.Empty;

    public List<MenuPermisoItemViewModel> Menus { get; set; } = new();
}

public sealed class MenuPermisoItemViewModel
{
    public int MenuId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? ControllerNombre { get; set; }
    public int Profundidad { get; set; }
    public string? DestinoNombre { get; set; }
    public string ContextoJerarquia { get; set; } = string.Empty;
    public bool PuedeVer { get; set; }
    public bool PuedeCrear { get; set; }
    public bool PuedeEditar { get; set; }
    public bool PuedeEliminar { get; set; }
    public bool PuedeProcesar { get; set; }
}

public sealed class MenuAdminItemViewModel
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Icono { get; init; }
    public string? Destino { get; init; }
    public string? DestinoNombre { get; init; }
    public int? IdMenuPadre { get; init; }
    public string? NombrePadre { get; init; }
    public int Orden { get; init; }
    public int Profundidad { get; init; }
    public bool Activo { get; init; }
    public bool TieneHijosActivos { get; init; }
}

public sealed class MenuAdminFormViewModel
{
    public int? IdMenu { get; set; }
    [Required, StringLength(64), RegularExpression("^[a-z][a-z0-9-]*$")]
    public string Codigo { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Nombre { get; set; } = string.Empty;
    [StringLength(32)]
    public string? Icono { get; set; }
    public int? IdMenuPadre { get; set; }
    public int Orden { get; set; }
    public string? Destino { get; set; }
}

public sealed record MenuParentOptionViewModel(int Id, string Nombre, int Profundidad);

public sealed record CatalogoSunatDefinicion(
    string Codigo,
    string Nombre,
    string Categoria,
    string Descripcion,
    string Accion,
    bool PermiteActivar);

public sealed record CatalogoSunatFilaViewModel(
    int Id,
    string? Codigo,
    string Descripcion,
    string? Detalle,
    bool? Activo,
    bool Bloqueado = false,
    string? MotivoBloqueo = null);

public sealed class CatalogoSunatPaginaViewModel
{
    public CatalogoSunatDefinicion Catalogo { get; init; } = default!;
    public string? Busqueda { get; init; }
    public int Pagina { get; init; }
    public int Paginas { get; init; }
    public int Total { get; init; }
    public IReadOnlyList<CatalogoSunatFilaViewModel> Filas { get; init; } = Array.Empty<CatalogoSunatFilaViewModel>();
}
