using System.Text.RegularExpressions;

namespace ProyectoFinal.Services;

public static class ValidadorDocumentoIdentidad
{
    public static string? Normalizar(string? numero) => numero?.Trim();

    public static bool EsValidoCliente(string? numero, string codigo) =>
        numero is not null && Regex.IsMatch(numero,
            codigo == "1" ? @"^[0-9]{8}$" : @"^[0-9]{11}$", RegexOptions.CultureInvariant);

    public static bool EsValidoProveedor(string? numero, string codigo, out int longitud)
    {
        longitud = codigo switch { "1" => 8, "4" => 9, _ => 11 };
        return numero is not null && Regex.IsMatch(numero, $@"^[0-9]{{{longitud}}}$", RegexOptions.CultureInvariant);
    }
}
