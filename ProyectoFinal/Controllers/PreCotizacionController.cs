using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public sealed class PreCotizacionController(ApplicationDbContext context, ISolicitudExtractor extractor, SugerenciaPrecioService precios) : Controller
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
            // Exact unique name only: similar or ambiguous names require human selection.
            var matches = model.Productos.Where(product => string.Equals(
                Normalizar(product.Nombre), Normalizar(line.NombreSugerido), StringComparison.Ordinal)).ToList();
            if (matches.Count == 1) line.ProductoId = matches[0].IdProducto;
        }
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

        if (model.ClienteId is null || !model.Clientes.Any(client => client.IdCliente == model.ClienteId))
            ModelState.AddModelError(nameof(model.ClienteId), "Selecciona un cliente registrado.");
        if (model.Lineas.Count is < 1 or > 10)
            ModelState.AddModelError(nameof(model.Lineas), "Revisa entre 1 y 10 líneas del trabajo.");
        else
        {
            for (var index = 0; index < model.Lineas.Count; index++)
            {
                var line = model.Lineas[index];
                if (line.ProductoId is null || !model.Productos.Any(product => product.IdProducto == line.ProductoId))
                    ModelState.AddModelError($"Lineas[{index}].ProductoId", "Selecciona un producto del catálogo.");
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
            Lineas = model.Lineas.Select(line => new CotizacionLineaInputViewModel
            {
                ProductoId = line.ProductoId,
                Cantidad = line.Cantidad,
                PrecioUnitario = model.Productos.Single(product => product.IdProducto == line.ProductoId).Precio ?? 0m
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
        model.Clientes = await context.Cliente.AsNoTracking().OrderBy(client => client.Nombre).ToListAsync();
        model.Productos = await context.Producto.AsNoTracking().OrderBy(product => product.Nombre).ToListAsync();
    }

    private static string Normalizar(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var normalized = name.Trim().Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder();
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                buffer.Append(char.ToUpperInvariant(character));
        return buffer.ToString().Normalize(NormalizationForm.FormC);
    }
}
