using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public sealed class CatalogosSunatController(CatalogoSunatService catalogos, IMenuAccessService menuAccess) : Controller
{
    public IActionResult Index() => View(CatalogoSunatRegistry.Catalogos
        .GroupBy(item => item.Categoria)
        .OrderBy(group => CatalogoSunatRegistry.Catalogos.ToList().FindIndex(item => item.Categoria == group.Key))
        .Select(group => new CatalogoSunatGrupoViewModel(group.Key, group.ToList())).ToList());

    public Task<IActionResult> TiposDocumentoIdentidad(string? q, int pagina = 1) => Mostrar("documentos-identidad", q, pagina);
    public Task<IActionResult> TiposComprobante(string? q, int pagina = 1) => Mostrar("tipos-comprobante", q, pagina);
    public Task<IActionResult> Monedas(string? q, int pagina = 1) => Mostrar("monedas", q, pagina);
    public Task<IActionResult> UnidadesMedida(string? q, int pagina = 1) => Mostrar("unidades-medida", q, pagina);
    public Task<IActionResult> Ubigeos(string? q, int pagina = 1) => Mostrar("ubigeos", q, pagina);
    public Task<IActionResult> Detracciones(string? q, int pagina = 1) => Mostrar("detracciones", q, pagina);
    public Task<IActionResult> MediosPago(string? q, int pagina = 1) => Mostrar("medios-pago", q, pagina);
    public Task<IActionResult> MotivosNotaCredito(string? q, int pagina = 1) => Mostrar("motivos-nc", q, pagina);
    public Task<IActionResult> MotivosNotaDebito(string? q, int pagina = 1) => Mostrar("motivos-nd", q, pagina);
    public Task<IActionResult> MotivosTraslado(string? q, int pagina = 1) => Mostrar("motivos-traslado", q, pagina);
    public Task<IActionResult> TiposEntidadFinanciera(string? q, int pagina = 1) => Mostrar("tipos-entidad-financiera", q, pagina);
    public Task<IActionResult> EntidadesFinancieras(string? q, int pagina = 1) => Mostrar("entidades-financieras", q, pagina);
    public Task<IActionResult> TiposActividad(string? q, int pagina = 1) => Mostrar("tipos-actividad", q, pagina);
    public Task<IActionResult> TiposContribuyente(string? q, int pagina = 1) => Mostrar("tipos-contribuyente", q, pagina);
    public Task<IActionResult> TiposVia(string? q, int pagina = 1) => Mostrar("tipos-via", q, pagina);
    public Task<IActionResult> TiposZona(string? q, int pagina = 1) => Mostrar("tipos-zona", q, pagina);
    public Task<IActionResult> EstadosEnvio(string? q, int pagina = 1) => Mostrar("estados-envio", q, pagina);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(string catalogo, int id, bool activo, string? q, int pagina = 1)
    {
        if (!CatalogoSunatRegistry.TryGet(catalogo, out var definition) || !definition.PermiteActivar)
            return NotFound();
        if (!MenuDestinationRegistry.TryGetForRoute("CatalogosSunat", definition.Accion, out var destination)
            || !await menuAccess.HasDestinationPermissionAsync(User, destination.Codigo, MenuPermissionKind.Process))
            return Forbid();

        var result = await catalogos.CambiarEstadoAsync(catalogo, id, activo, User);
        TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;
        return RedirectToAction(definition.Accion, new { q, pagina });
    }

    private async Task<IActionResult> Mostrar(string codigo, string? q, int pagina)
    {
        var result = await catalogos.ObtenerPaginaAsync(codigo, q, pagina);
        if (result is null) return NotFound();
        ViewBag.PuedeProcesar = MenuDestinationRegistry.TryGetForRoute("CatalogosSunat", result.Catalogo.Accion, out var destination)
            && await menuAccess.HasDestinationPermissionAsync(User, destination.Codigo, MenuPermissionKind.Process);
        return View("Catalogo", result);
    }
}

public sealed record CatalogoSunatGrupoViewModel(string Nombre, IReadOnlyList<CatalogoSunatDefinicion> Catalogos);
