using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize]
public class ProductoesController(IProductosCatalogoService productos) : Controller
{
    public async Task<IActionResult> Index(int? almacenId)
    {
        var data = await productos.ListarAsync(almacenId);
        if (data is null) return NotFound();
        ViewData["Almacenes"] = data.Almacenes;
        ViewData["AlmacenSeleccionadoId"] = data.AlmacenSeleccionado?.IdAlmacen;
        ViewData["AlmacenSeleccionadoNombre"] = data.AlmacenSeleccionado?.Nombre;
        ViewData["ModoInventario"] = data.AlmacenSeleccionado is null ? "Legado"
            : data.AlmacenSeleccionado.FechaActivacionUtc is not null ? "Activo" : "CortePendiente";
        ViewData["SaldosInventario"] = data.Saldos;
        return View(data.Productos);
    }

    public async Task<IActionResult> Details(int? id, int? almacenId)
    {
        if (id is null) return NotFound();
        var data = await productos.ObtenerDetalleAsync(id.Value, almacenId);
        if (data is null) return NotFound();
        ViewData["Almacenes"] = data.Almacenes;
        ViewData["AlmacenSeleccionadoId"] = data.AlmacenSeleccionado?.IdAlmacen;
        ViewData["AlmacenSeleccionadoNombre"] = data.AlmacenSeleccionado?.Nombre;
        ViewData["StockActual"] = data.StockActual;
        ViewData["PresentacionesProducto"] = data.Presentaciones;
        ViewData["UnidadesMedida"] = data.Unidades;
        return View(data.Producto);
    }

    [HttpGet]
    public async Task<IActionResult> StockPorAlmacen(int id)
    {
        var model = await productos.ObtenerStockPorAlmacenAsync(id);
        return model is null ? NotFound() : PartialView("_StockPorAlmacen", model);
    }

    public async Task<IActionResult> Create()
    {
        await LoadProductFormOptionsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Nombre,Descripcion,Precio,CostoUnitarioSinIgv,IdProveedor,EsServicio,IdUnidadMedidaBase")] Producto producto)
    {
        producto.Stock = 0;
        ValidateProduct(producto);
        if (ModelState.IsValid)
        {
            var result = await productos.CrearAsync(producto);
            if (result.Estado == ResultadoProductoEstado.Exito) return RedirectToAction(nameof(Index));
            AddErrors(result);
        }
        await LoadProductFormOptionsAsync(producto.IdProveedor, producto.IdUnidadMedidaBase);
        return View(producto);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var producto = await productos.ObtenerAsync(id.Value);
        if (producto is null) return NotFound();
        await LoadProductFormOptionsAsync(producto.IdProveedor, producto.IdUnidadMedidaBase);
        return View(producto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarPresentacion(int id, PresentacionProductoFormViewModel model)
    {
        var result = await productos.AgregarPresentacionAsync(id, model, User);
        if (result.Estado == ResultadoProductoEstado.NoEncontrado) return NotFound();
        if (result.Estado == ResultadoProductoEstado.Exito)
            TempData["Success"] = "Se agregó la presentación del producto.";
        else TempData["Error"] = ErrorText(result);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoPresentacion(int id, int presentacionId)
    {
        var result = await productos.CambiarEstadoPresentacionAsync(id, presentacionId, User);
        if (result.Estado == ResultadoProductoEstado.NoEncontrado) return NotFound();
        if (result.Estado == ResultadoProductoEstado.Exito)
        {
            var detail = await productos.ObtenerDetalleAsync(id, null);
            var active = detail?.Presentaciones.FirstOrDefault(item => item.IdPresentacionProducto == presentacionId)?.Activo == true;
            TempData["Success"] = active ? "Presentación activada." : "Presentación desactivada.";
        }
        else TempData["Error"] = ErrorText(result);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("IdProducto,Nombre,Descripcion,Precio,CostoUnitarioSinIgv,IdProveedor,EsServicio,IdUnidadMedidaBase,RowVersion")] Producto producto)
    {
        if (id != producto.IdProducto) return NotFound();
        ValidateProduct(producto);
        if (ModelState.IsValid)
        {
            var result = await productos.ActualizarAsync(id, producto);
            if (result.Estado == ResultadoProductoEstado.Exito) return RedirectToAction(nameof(Index));
            if (result.Estado == ResultadoProductoEstado.NoEncontrado) return NotFound();
            if (result.Estado == ResultadoProductoEstado.Conflicto)
            {
                producto = result.Producto!;
                ModelState.Clear();
                ModelState.AddModelError(string.Empty,
                    "El producto cambió mientras editabas (posiblemente por una venta). Revisa los datos actualizados y vuelve a guardar.");
            }
            else AddErrors(result);
        }
        await LoadProductFormOptionsAsync(producto.IdProveedor, producto.IdUnidadMedidaBase);
        return View(producto);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var producto = await productos.ObtenerParaEliminarAsync(id.Value);
        return producto is null ? NotFound() : View(producto);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await productos.EliminarAsync(id);
        if (result.Estado == ResultadoProductoEstado.NoEncontrado) return NotFound();
        if (result.Estado == ResultadoProductoEstado.EnUso)
        {
            ModelState.AddModelError(string.Empty, "No se puede eliminar un producto utilizado en cotizaciones o pedidos.");
            return View("Delete", result.Producto);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadProductFormOptionsAsync(int? proveedorId = null, int? unidadBaseId = null)
    {
        var (proveedores, unidades) = await productos.ObtenerOpcionesAsync();
        ViewData["IdProveedor"] = new SelectList(proveedores, "IdProveedor", "Nombre", proveedorId);
        ViewData["UnidadesMedida"] = new SelectList(unidades, "IdUnidadMedida", "Descripcion", unidadBaseId);
    }

    private void AddErrors(ResultadoProducto result)
    {
        foreach (var (key, message) in result.Errores ?? new Dictionary<string, string>())
            ModelState.AddModelError(key, message);
    }

    private static string ErrorText(ResultadoProducto result) => string.Join(" ",
        result.Errores?.Values ?? Array.Empty<string>());

    private void ValidateProduct(Producto producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre))
            ModelState.AddModelError(nameof(Producto.Nombre), "Ingresa el nombre del producto.");
        if (producto.Precio is < 0)
            ModelState.AddModelError(nameof(Producto.Precio), "El precio no puede ser negativo.");
        if (producto.CostoUnitarioSinIgv is < 0)
            ModelState.AddModelError(nameof(Producto.CostoUnitarioSinIgv), "El costo no puede ser negativo.");
        if (producto.CostoUnitarioSinIgv is { } costo && decimal.Round(costo, 2) != costo)
            ModelState.AddModelError(nameof(Producto.CostoUnitarioSinIgv), "El costo admite como máximo dos decimales.");
        if (producto.Stock is < 0)
            ModelState.AddModelError(nameof(Producto.Stock), "El stock no puede ser negativo.");
    }
}
