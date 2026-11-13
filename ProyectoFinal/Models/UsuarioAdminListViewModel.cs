namespace ProyectoFinal.Models;

public sealed class UsuarioAdminListViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool Activo { get; init; } = true;
    public bool EsCuentaPropia { get; init; }
    public bool CorreoConfirmado { get; init; }
}

public sealed class UsuarioCrearViewModel
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress,
     System.ComponentModel.DataAnnotations.StringLength(256)]
    public string Correo { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8),
     System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Compare(nameof(Password)),
     System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    public string ConfirmarPassword { get; set; } = string.Empty;
}
