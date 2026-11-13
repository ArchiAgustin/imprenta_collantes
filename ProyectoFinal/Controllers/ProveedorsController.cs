using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers
{
    [Authorize]
    public class ProveedorsController : Controller
    {
        private readonly IProveedoresCatalogoService _catalogo;
        private readonly IdentificacionService _identificacion;

        public ProveedorsController(IProveedoresCatalogoService catalogo, IdentificacionService identificacion)
        {
            _catalogo = catalogo;
            _identificacion = identificacion;
        }

        // GET: Proveedors
        public async Task<IActionResult> Index(string? nombre, string? documento, string? contacto, int pagina = 1)
        {
            nombre = NormalizarFiltro(nombre);
            documento = NormalizarFiltro(documento);
            contacto = NormalizarFiltro(contacto);
            var result = await _catalogo.ListarAsync(nombre, documento, contacto, pagina);
            ViewBag.NombreFiltro = nombre;
            ViewBag.DocumentoFiltro = documento;
            ViewBag.ContactoFiltro = contacto;
            ViewBag.Pagina = result.Pagina;
            ViewBag.Paginas = result.Paginas;
            ViewBag.TotalProveedores = result.Total;
            return View(result.Items);
        }

        // GET: Proveedors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proveedor = await _catalogo.ObtenerDetalleAsync(id.Value);
            if (proveedor == null)
            {
                return NotFound();
            }

            return View(proveedor);
        }

        // GET: Proveedors/Create
        public async Task<IActionResult> Create()
        {
            await _identificacion.CargarCatalogosAsync(this, null);
            return View();
        }

        // POST: Proveedors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Contacto,Ruc,IdDocumentoIdentidadTipo,NumeroDocumento,Direccion,UbigeoCodigo")] Proveedor proveedor)
        {
            await _identificacion.ValidarProveedorAsync(proveedor, null, ModelState);
            if (ModelState.IsValid)
            {
                await _catalogo.CrearAsync(proveedor);
                return RedirectToAction(nameof(Index));
            }
            await _identificacion.CargarCatalogosAsync(this, proveedor.UbigeoCodigo);
            return View(proveedor);
        }

        // GET: Proveedors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proveedor = await _catalogo.ObtenerAsync(id.Value);
            if (proveedor == null)
            {
                return NotFound();
            }
            await _identificacion.CargarCatalogosAsync(this, proveedor.UbigeoCodigo);
            return View(proveedor);
        }

        // POST: Proveedors/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdProveedor,Nombre,Contacto,Ruc,IdDocumentoIdentidadTipo,NumeroDocumento,Direccion,UbigeoCodigo")] Proveedor proveedor)
        {
            if (id != proveedor.IdProveedor)
            {
                return NotFound();
            }

            var existing = await _catalogo.ObtenerAsync(id);
            if (existing is null) return NotFound();
            await _identificacion.ValidarProveedorAsync(proveedor, id, ModelState);
            if (ModelState.IsValid)
            {
                try
                {
                    await _catalogo.ActualizarAsync(id, proveedor);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
                {
                    if (!await _catalogo.ExisteAsync(proveedor.IdProveedor))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            await _identificacion.CargarCatalogosAsync(this, proveedor.UbigeoCodigo);
            return View(proveedor);
        }

        // GET: Proveedors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proveedor = await _catalogo.ObtenerParaEliminarAsync(id.Value);
            if (proveedor == null)
            {
                return NotFound();
            }

            return View(proveedor);
        }

        // POST: Proveedors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var outcome = await _catalogo.EliminarAsync(id);
            if (outcome == ResultadoEliminarCatalogo.NoEncontrado) return NotFound();
            if (outcome == ResultadoEliminarCatalogo.EnUso)
            {
                ModelState.AddModelError(string.Empty, "No se puede eliminar un proveedor con productos asociados.");
                return View("Delete", await _catalogo.ObtenerParaEliminarAsync(id));
            }
            return RedirectToAction(nameof(Index));
        }

        private static string? NormalizarFiltro(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
