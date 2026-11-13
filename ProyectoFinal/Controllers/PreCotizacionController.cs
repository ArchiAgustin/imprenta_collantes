using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public sealed class PreCotizacionController(IPreCotizacionCatalogoService catalogo, ISolicitudExtractor extractor, SugerenciaPrecioService precios) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new PreCotizacionViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Preparar(PreCotizacionViewModel model)
    {
        model.Analizada = false;
        model.Preparada = false;
        if (string.IsNullOrWhiteSpace(model.Solicitud) || model.Solicitud.Length > 4000)
        {
            ModelState.AddModelError(nameof(model.Solicitud), "Escribe una solicitud de hasta 4000 caracteres.");
            return View("Index", model);
        }

        var identidad = ProteccionSolicitudService.ExtraerIdentidadLocal(model.Solicitud);
        model.ClienteNombreSugerido = identidad.Nombre;
        model.ClienteDocumentoTipoSugerido = identidad.CodigoDocumento;
        model.ClienteDocumentoNumeroSugerido = identidad.NumeroDocumento;
        model.ConfirmarIdentidadLocal = false;
        model.TextoRevisado = ProteccionSolicitudService.Anonimizar(model.Solicitud);
        model.Solicitud = null; // Never forward or embed the unredacted message in the next step.
        ModelState.Clear();
        model.Preparada = true;
        if (model.TextoRevisado.Length < 10)
            ModelState.AddModelError(nameof(model.TextoRevisado), "No quedan detalles del trabajo. Escríbelos sin datos personales.");
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("groq")]
    public async Task<IActionResult> Analizar(PreCotizacionViewModel model, CancellationToken cancellationToken)
    {
        model.Preparada = true;
        model.Analizada = false;
        model.Lineas = new();
        model.Solicitud = null;
        if (!model.ConfirmarIdentidadLocal)
        {
            model.ClienteNombreSugerido = null;
            model.ClienteDocumentoTipoSugerido = null;
            model.ClienteDocumentoNumeroSugerido = null;
            model.ClienteIdSugerido = null;
        }
        else
        {
            model.ClienteNombreSugerido = model.ClienteNombreSugerido?.Trim();
            model.ClienteDocumentoNumeroSugerido = model.ClienteDocumentoNumeroSugerido?.Trim();
            if (string.IsNullOrWhiteSpace(model.ClienteNombreSugerido) && string.IsNullOrWhiteSpace(model.ClienteDocumentoNumeroSugerido))
                ModelState.AddModelError(nameof(model.ConfirmarIdentidadLocal), "No hay datos de identidad detectados para confirmar.");
            if (model.ClienteDocumentoTipoSugerido is { } codigo)
            {
                var longitud = codigo == "1" ? 8 : codigo == "6" ? 11 : 0;
                if (longitud == 0 || model.ClienteDocumentoNumeroSugerido is null
                    || !System.Text.RegularExpressions.Regex.IsMatch(model.ClienteDocumentoNumeroSugerido, $@"^[0-9]{{{longitud}}}$"))
                    ModelState.AddModelError(nameof(model.ClienteDocumentoNumeroSugerido), "El documento detectado no tiene un formato DNI/RUC válido.");
            }
        }
        if (string.IsNullOrWhiteSpace(model.TextoRevisado) || model.TextoRevisado.Length is < 10 or > 4000)
            ModelState.AddModelError(nameof(model.TextoRevisado), "Revisa un texto de entre 10 y 4000 caracteres.");
        if (!model.TextoAprobado)
            ModelState.AddModelError(nameof(model.TextoAprobado), "Confirma que el texto no incluya datos personales.");

        var outgoing = ProteccionSolicitudService.Anonimizar(model.TextoRevisado ?? "");
        if (outgoing != model.TextoRevisado?.Trim())
        {
            model.TextoRevisado = outgoing;
            model.TextoAprobado = false;
            ModelState.Remove(nameof(model.TextoRevisado));
            ModelState.Remove(nameof(model.TextoAprobado));
            ModelState.AddModelError(nameof(model.TextoRevisado), "Se ocultaron más datos. Revisa de nuevo el texto antes de enviarlo.");
        }

        if (!ModelState.IsValid) return View("Index", model);

        var result = await extractor.ExtraerAsync(outgoing, cancellationToken);
        if (!result.Exito || result.Datos is null)
        {
            model.Aviso = result.Mensaje;
            return View("Index", model);
        }

        model.Preparada = false;
        model.Analizada = true;
        model.TextoRevisado = null;
        model.Observaciones = result.Datos.Observaciones;
        model.FechaRequerida = result.Datos.FechaRequerida;
        model.Lineas = result.Datos.Lineas.Select(line => new PreCotizacionLineaViewModel
        {
            NombreSugerido = line.Nombre,
            Cantidad = line.Cantidad,
            Especificacion = line.Especificacion
        }).ToList();
        var description = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(model.Observaciones)) description.AppendLine(model.Observaciones);
        if (!string.IsNullOrWhiteSpace(model.FechaRequerida)) description.AppendLine($"Fecha solicitada de entrega: {model.FechaRequerida}");
        foreach (var line in model.Lineas.Where(line => !string.IsNullOrWhiteSpace(line.Especificacion)))
            description.AppendLine($"{line.NombreSugerido}: {line.Especificacion}");
        var proposal = description.ToString().Trim();
        model.Descripcion = proposal[..Math.Min(4000, proposal.Length)];
        await CargarCatalogoAsync(model);

        foreach (var line in model.Lineas)
        {
            var exactMatches = model.Productos.Where(product => string.Equals(
                Normalizar(product.Nombre), Normalizar(line.NombreSugerido), StringComparison.Ordinal)).ToList();
            line.Candidatos = BuscarCandidatos(line.NombreSugerido, model.Productos);
            // Only a single exact product-name match may be preselected. Similar or
            // duplicate names remain suggestions for the seller to resolve.
            if (exactMatches.Count == 1)
            {
                var exact = exactMatches[0];
                line.ProductoId = exact.IdProducto;
                line.IdPresentacionProducto = exact.EsServicio ? null : exact.Presentaciones.FirstOrDefault(item =>
                    item.IdUnidadMedida == exact.IdUnidadMedidaBase && item.Factor == 1m)?.IdPresentacionProducto;
            }
        }
        if (model.ClienteId is null && model.ClienteIdSugerido is { } match
            && model.Clientes.Any(client => client.IdCliente == match))
            model.ClienteId = match;
        ModelState.Clear();
        model.Aviso = "Ninguna sugerencia crea una cotización. Selecciona el cliente, confirma los productos y revisa los precios en el siguiente formulario.";
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cargar(PreCotizacionViewModel model)
    {
        model.Analizada = true;
        model.Solicitud = null;
        model.TextoRevisado = null;
        model.Lineas ??= new();
        await CargarCatalogoAsync(model);
        foreach (var line in model.Lineas)
            line.Candidatos = BuscarCandidatos(line.NombreSugerido, model.Productos);

        if (model.ClienteId is null || !model.Clientes.Any(client => client.IdCliente == model.ClienteId))
            ModelState.AddModelError(nameof(model.ClienteId), "Selecciona un cliente registrado.");
        if (model.Lineas.Count is < 1 or > 10)
            ModelState.AddModelError(nameof(model.Lineas), "Revisa entre 1 y 10 líneas del trabajo.");
        else
        {
            for (var index = 0; index < model.Lineas.Count; index++)
            {
                var line = model.Lineas[index];
                var selectedProduct = line.ProductoId is null
                    ? null
                    : model.Productos.FirstOrDefault(product => product.IdProducto == line.ProductoId);
                if (selectedProduct is null)
                    ModelState.AddModelError($"Lineas[{index}].ProductoId", "Selecciona un producto del catálogo.");
                else if (selectedProduct.EsServicio && line.IdPresentacionProducto is not null
                    || !selectedProduct.EsServicio && line.IdPresentacionProducto is { } selectedPresentation
                        && !selectedProduct.Presentaciones.Any(presentation => presentation.IdPresentacionProducto == selectedPresentation))
                    ModelState.AddModelError($"Lineas[{index}].IdPresentacionProducto", "Selecciona una presentación activa del producto.");
                if (line.Cantidad is < 1 or > 1_000_000)
                    ModelState.AddModelError($"Lineas[{index}].Cantidad", "La cantidad debe estar entre 1 y 1 000 000.");
            }
        }
        if (model.Descripcion?.Length > 4000) ModelState.AddModelError(nameof(model.Descripcion), "La descripción es demasiado larga.");
        if (!ModelState.IsValid) return View("Index", model);

        var quote = new CotizacionFormViewModel
        {
            ClienteId = model.ClienteId,
            Descripcion = model.Descripcion?.Trim(),
            FechaEmision = DateOnly.FromDateTime(DateTime.UtcNow),
            Usuario = User.Identity?.Name ?? string.Empty,
            Clientes = model.Clientes,
            Productos = model.Productos,
            Lineas = model.Lineas.Select(line =>
            {
                var product = model.Productos.Single(item => item.IdProducto == line.ProductoId);
                var presentation = product.EsServicio ? null
                    : line.IdPresentacionProducto is { } selectedId
                        ? product.Presentaciones.FirstOrDefault(item => item.IdPresentacionProducto == selectedId)
                        : product.Presentaciones.FirstOrDefault(item => item.IdUnidadMedida == product.IdUnidadMedidaBase && item.Factor == 1m);
                var factor = presentation?.Factor ?? 1m;
                return new CotizacionLineaInputViewModel
                {
                    ProductoId = line.ProductoId,
                    IdPresentacionProducto = presentation?.IdPresentacionProducto,
                    FactorPresentacion = factor,
                    PresentacionDescripcion = product.EsServicio ? "Servicio"
                        : presentation?.Descripcion ?? product.UnidadMedidaBase?.DescripcionCorta ?? product.UnidadMedidaBase?.Descripcion ?? "Unidad base",
                    UnidadBaseDescripcion = product.UnidadMedidaBase?.Descripcion,
                    Cantidad = line.Cantidad,
                    PrecioUnitario = (product.Precio ?? 0m) * factor
                };
            }).ToList()
        };
        quote.RecalcularTotales();
        quote.ReferenciasPrecio = await precios.ObtenerAsync(model.ClienteId!.Value, quote.Lineas);
        ModelState.Clear();
        // The existing Create POST remains the sole operation that persists a quote.
        return View("~/Views/Cotizacions/Create.cshtml", quote);
    }

    private async Task CargarCatalogoAsync(PreCotizacionViewModel model)
    {
        await catalogo.CargarAsync(model);
    }

    private static List<PreCotizacionProductoCandidato> BuscarCandidatos(
        string? nombre,
        IReadOnlyCollection<Producto> productos)
    {
        var tokens = Normalizar(nombre).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        if (tokens.Count == 0) return new();
        return productos.Select(product =>
            {
                var nombreNormalizado = Normalizar(product.Nombre);
                var descripcionNormalizada = Normalizar(product.Descripcion);
                var nombreTokens = nombreNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                var descripcionTokens = descripcionNormalizada.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                var nombreExacto = nombreNormalizado == Normalizar(nombre);
                var nombreCoincide = tokens.Intersect(nombreTokens, StringComparer.Ordinal).Count();
                var descripcionCoincide = tokens.Intersect(descripcionTokens, StringComparer.Ordinal).Count();
                var coincidencias = Math.Max(nombreCoincide, descripcionCoincide);
                var razon = nombreExacto ? "Coincidencia exacta por nombre"
                    : nombreCoincide > 0 ? "Coincidencia por términos del nombre"
                    : "Coincidencia por términos de la descripción";
                return new
                {
                    Producto = product,
                    Exacto = nombreExacto,
                    Coincidencias = coincidencias,
                    Razon = razon
                };
            })
            .Where(item => item.Exacto || item.Coincidencias > 0
                && (decimal)item.Coincidencias / tokens.Count >= .5m)
            .OrderByDescending(item => item.Exacto)
            .ThenByDescending(item => item.Coincidencias)
            .ThenBy(item => item.Producto.Nombre)
            .Take(5)
            .Select(item => new PreCotizacionProductoCandidato(
                item.Producto.IdProducto,
                $"{(item.Producto.EsServicio ? "Servicio" : "Artículo")} · {item.Producto.Nombre}",
                item.Producto.EsServicio ? "Servicio" : "Artículo",
                item.Razon))
            .ToList();
    }

    private static string Normalizar(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var normalized = name.Trim().Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder();
        var previousWasSpace = true;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                buffer.Append(char.ToUpperInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                buffer.Append(' ');
                previousWasSpace = true;
            }
        }
        return buffer.ToString().Normalize(NormalizationForm.FormC);
    }
}
