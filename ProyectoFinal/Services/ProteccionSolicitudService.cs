using System.Text.RegularExpressions;

namespace ProyectoFinal.Services;

public static partial class ProteccionSolicitudService
{
    // Defense in depth only: names and free-form addresses cannot be reliably detected
    // with regular expressions. A human must inspect the exact text before any API call.
    public static string Anonimizar(string texto)
    {
        var result = Email().Replace(texto, "[CORREO]");
        result = Url().Replace(result, "[ENLACE]");
        result = LabeledIdentity().Replace(result, "$1 [DATO PERSONAL]");
        result = DocumentNumber().Replace(result, "[DOCUMENTO]");
        result = Telephone().Replace(result, "[TELÉFONO]");
        result = Mention().Replace(result, "[USUARIO]");
        return result.Trim();
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"\b(?:https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Url();

    [GeneratedRegex(@"(?im)\b((?:nombre|cliente|contacto|direcci[oó]n|domicilio|raz[oó]n social|dni|ruc)\s*:)\s*[^\r\n;,]+")]
    private static partial Regex LabeledIdentity();

    [GeneratedRegex(@"(?<!\d)(?:\d{8}|\d{11})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentNumber();

    [GeneratedRegex(@"(?<!\d)(?:\+?51[\s.-]?)?9\d{2}[\s.-]?\d{3}[\s.-]?\d{3}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Telephone();

    [GeneratedRegex(@"(?<!\w)@[\w.]+", RegexOptions.CultureInvariant)]
    private static partial Regex Mention();
}
