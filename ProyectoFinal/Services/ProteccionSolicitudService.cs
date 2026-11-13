using System.Text.RegularExpressions;

namespace ProyectoFinal.Services;

public static partial class ProteccionSolicitudService
{
    public sealed record IdentidadClienteDetectada(string? Nombre, string? CodigoDocumento, string? NumeroDocumento);

    public static IdentidadClienteDetectada ExtraerIdentidadLocal(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return new(null, null, null);
        var nameMatch = LabeledName().Match(texto);
        var nombre = nameMatch.Success ? nameMatch.Groups["nombre"].Value.Trim().Trim('.', ':', '-', ' ') : null;
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100) nombre = null;

        var ruc = LabeledRuc().Match(texto);
        if (ruc.Success) return new(nombre, "6", ruc.Groups["numero"].Value);
        var dni = LabeledDni().Match(texto);
        if (dni.Success) return new(nombre, "1", dni.Groups["numero"].Value);
        return new(nombre, null, null);
    }

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

    [GeneratedRegex(@"(?im)\b(?:cliente|contacto|nombre(?:\s+del\s+cliente)?|raz[oó]n\s+social)\s*[:=]\s*(?<nombre>[^\r\n;,|]{2,120})")]
    private static partial Regex LabeledName();

    [GeneratedRegex(@"(?i)\bRUC\s*(?:N(?:ú|u)mero\s*)?(?:[:=#-]\s*)?(?<numero>\d{11})\b", RegexOptions.CultureInvariant)]
    private static partial Regex LabeledRuc();

    [GeneratedRegex(@"(?i)\bDNI\s*(?:N(?:ú|u)mero\s*)?(?:[:=#-]\s*)?(?<numero>\d{8})\b", RegexOptions.CultureInvariant)]
    private static partial Regex LabeledDni();

    [GeneratedRegex(@"(?<!\d)(?:\d{8}|\d{11})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentNumber();

    [GeneratedRegex(@"(?<!\d)(?:\+?51[\s.-]?)?9\d{2}[\s.-]?\d{3}[\s.-]?\d{3}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Telephone();

    [GeneratedRegex(@"(?<!\w)@[\w.]+", RegexOptions.CultureInvariant)]
    private static partial Regex Mention();
}
