using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers
{
    [Authorize]
    public class ProductoesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly InventarioService _inventario;

        public ProductoesController(ApplicationDbContext context, InventarioService inventario)
        {
            _context = context;
            _inventario = inventario;
        }

        // GET: Productoes
        public async Task<IActionResult> Index()
        {
            var principal = await _context.Almacenes.AsNoTracking().FirstOrDefaultAsync(item => item.Nombre == InventarioService.NombreAlmacen);
            ViewData["InventarioActivo"] = principal?.FechaActivacionUtc is not null;
            ViewData["SaldosInventario"] = principal?.FechaActivacionUtc is null
                ? new Dictionary<int, decimal>()
                : await _context.SaldosAlmacen.AsNoTracking().Where(item => item.IdAlmacen == principal.IdAlmacen)
                    .ToDictionaryAsync(item => item.IdProducto, item => item.CantidadBase);
            var applicationDbContext = _context.Producto.Include(p => p.IdProveedorNavigation);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Productoes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Producto
                .Include(p => p.IdProveedorNavigation)
                .FirstOrDefaultAsync(m => m.IdProducto == id);
            if (producto == null)
            {
                return NotFound();
            }

            var principal = await _context.Almacenes.AsNoTracking().FirstOrDefaultAsync(item => item.Nombre == InventarioService.NombreAlmacen);
            var active = principal?.FechaActivacionUtc is not null;
            var balance = active ? await _context.SaldosAlmacen.AsNoTracking()
                .FirstOrDefaultAsync(item => item.IdAlmacen == principal!.IdAlmacen && item.IdProducto == id) : null;
            ViewData["StockActual"] = producto.EsServicio ? "No aplica" : active
                ? balance is null ? "Sin saldo registrado" : balance.CantidadBase.ToString("0.###")
                : (producto.Stock ?? 0).ToString();

            return View(producto);
        }

        // GET: Productoes/Create
        public IActionResult Create()
        {
            ViewData["IdProveedor"] = new SelectList(_context.Proveedor, "IdProveedor", "Nombre");
            return View();
        }

        // POST: Productoes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Descripcion,Precio,CostoUnitarioSinIgv,IdProveedor,EsServicio")] Producto producto)
        {
            // Neither an untrusted POST nor the product editor establishes stock.
            producto.Stock = 0;
            ValidateProduct(producto);
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync() : null;
            var warehouse = await _inventario.BloquearPrincipalAsync();
            if (warehouse?.ConteoEnCurso == true)
                ModelState.AddModelError(string.Empty, "El conteo está en curso. Espera a que termine para crear artículos.");
            var unit = producto.EsServicio ? null : await _context.UnidadesMedida.AsNoTracking()
                .OrderBy(item => item.IdUnidadMedida)
                .FirstOrDefaultAsync(item => item.Activo && item.Descripcion == "UNIDAD (BIENES)");
            if (!producto.EsServicio && warehouse?.FechaActivacionUtc is not null && unit is null)
                ModelState.AddModelError(string.Empty, "No está configurada la unidad base para artículos.");
            if (ModelState.IsValid)
            {
                if (!producto.EsServicio && unit is not null)
                {
                    producto.IdUnidadMedidaBase = unit.IdUnidadMedida;
                    _context.PresentacionesProducto.Add(new PresentacionProducto
                    {
                        Producto = producto, IdUnidadMedida = unit.IdUnidadMedida,
                        Descripcion = "Unidad", Factor = 1m
                    });
                }
                _context.Add(producto);
                await _context.SaveChangesAsync();
                if (!producto.EsServicio && warehouse?.FechaActivacionUtc is not null)
                {
                    _context.SaldosAlmacen.Add(new StockAlmacen { IdAlmacen = warehouse.IdAlmacen, IdProducto = producto.IdProducto, CantidadBase = 0m });
                    await _context.SaveChangesAsync();
                }
                if (transaction is not null) await transaction.CommitAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdProveedor"] = new SelectList(_context.Proveedor, "IdProveedor", "Nombre", producto.IdProveedor);
            return View(producto);
        }

        // GET: Productoes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Producto.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }
            ViewData["IdProveedor"] = new SelectList(_context.Proveedor, "IdProveedor", "Nombre", producto.IdProveedor);
            return View(producto);
        }

        // POST: Productoes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdProducto,Nombre,Descripcion,Precio,CostoUnitarioSinIgv,IdProveedor,EsServicio,RowVersion")] Producto producto)
        {
            if (id != producto.IdProducto)
            {
                return NotFound();
            }

            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync() : null;
            var warehouse = await _inventario.BloquearPrincipalAsync();

            if (producto.RowVersion is null || producto.RowVersion.Length == 0)
                ModelState.AddModelError(string.Empty, "La versión del producto no es válida. Recarga el formulario antes de guardar.");

            var tipoActual = await _context.Producto.AsNoTracking()
                .Where(item => item.IdProducto == id)
                .Select(item => new { item.EsServicio, item.IdUnidadMedidaBase, item.Stock })
                .FirstOrDefaultAsync();
            if (tipoActual is null) return NotFound();
            // The current editor does not expose the base unit; don't overwrite a
            // value assigned during inventory preparation with a detached update.
            producto.IdUnidadMedidaBase = tipoActual.IdUnidadMedidaBase;
            producto.Stock = tipoActual.Stock;
            if (warehouse?.ConteoEnCurso == true)
                ModelState.AddModelError(string.Empty, "El conteo está en curso. Espera a que termine para editar artículos.");
            if (tipoActual.EsServicio != producto.EsServicio && await _context.DetalleVenta.AnyAsync(line => line.IdProducto == id))
                ModelState.AddModelError(nameof(Producto.EsServicio),
                    "No se puede cambiar el tipo de un producto que ya figura en ventas; registra uno nuevo.");
            if (tipoActual.EsServicio != producto.EsServicio &&
                (await _context.SaldosAlmacen.AnyAsync(item => item.IdProducto == id)
                 || await _context.PresentacionesProducto.AnyAsync(item => item.IdProducto == id)))
                ModelState.AddModelError(nameof(Producto.EsServicio), "No se puede cambiar el tipo de un artículo con presentación o saldo asociado.");
            ValidateProduct(producto);
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                    if (transaction is not null) await transaction.CommitAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    var latest = await _context.Producto.AsNoTracking()
                        .FirstOrDefaultAsync(item => item.IdProducto == id);
                    if (latest is null) return NotFound();
                    ModelState.Clear();
                    ModelState.AddModelError(string.Empty,
                        "El producto cambió mientras editabas (posiblemente por una venta). Revisa los datos actualizados y vuelve a guardar.");
                    ViewData["IdProveedor"] = new SelectList(_context.Proveedor, "IdProveedor", "Nombre", latest.IdProveedor);
                    return View(latest);
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdProveedor"] = new SelectList(_context.Proveedor, "IdProveedor", "Nombre", producto.IdProveedor);
            return View(producto);
        }

        // GET: Productoes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Producto
                .Include(p => p.IdProveedorNavigation)
                .FirstOrDefaultAsync(m => m.IdProducto == id);
            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        // POST: Productoes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync() : null;
            var warehouse = await _inventario.BloquearPrincipalAsync();
            var producto = await _context.Producto.FindAsync(id);
            if (producto is null) return NotFound();
            if (warehouse?.ConteoEnCurso == true)
            {
                ModelState.AddModelError(string.Empty, "No se pueden eliminar artículos durante el conteo.");
                return View("Delete", producto);
            }
            if (await _context.Set<DetalleCotizacion>().AnyAsync(d => d.IdProducto == id) ||
                await _context.Set<DetallePedido>().AnyAsync(d => d.IdProducto == id) ||
                await _context.DetalleVenta.AnyAsync(d => d.IdProducto == id) ||
                await _context.SaldosAlmacen.AnyAsync(d => d.IdProducto == id) ||
                await _context.DetallesCorteInventario.AnyAsync(d => d.IdProducto == id) ||
                await _context.Kardex.AnyAsync(d => d.IdProducto == id) ||
                await _context.PresentacionesProducto.AnyAsync(d => d.IdProducto == id))
            {
                await _context.Entry(producto).Reference(p => p.IdProveedorNavigation).LoadAsync();
                ModelState.AddModelError(string.Empty, "No se puede eliminar un producto utilizado en cotizaciones o pedidos.");
                return View("Delete", producto);
            }
            _context.Producto.Remove(producto);

            await _context.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductoExists(int id)
        {
            return _context.Producto.Any(e => e.IdProducto == id);
        }

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
}
