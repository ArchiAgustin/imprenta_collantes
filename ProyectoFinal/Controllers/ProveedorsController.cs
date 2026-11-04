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
    public class ProveedorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IdentificacionService _identificacion;

        public ProveedorsController(ApplicationDbContext context, IdentificacionService identificacion)
        {
            _context = context;
            _identificacion = identificacion;
        }

        // GET: Proveedors
        public async Task<IActionResult> Index()
        {
            return View(await _context.Proveedor.AsNoTracking().Include(item => item.Ubigeo).ToListAsync());
        }

        // GET: Proveedors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proveedor = await _context.Proveedor.Include(item => item.Ubigeo)
                .FirstOrDefaultAsync(m => m.IdProveedor == id);
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
        public async Task<IActionResult> Create([Bind("Nombre,Contacto,Ruc,Direccion,UbigeoCodigo")] Proveedor proveedor)
        {
            await _identificacion.ValidarProveedorAsync(proveedor, null, ModelState);
            if (ModelState.IsValid)
            {
                _context.Add(proveedor);
                await _context.SaveChangesAsync();
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

            var proveedor = await _context.Proveedor.FindAsync(id);
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
        public async Task<IActionResult> Edit(int id, [Bind("IdProveedor,Nombre,Contacto,Ruc,Direccion,UbigeoCodigo")] Proveedor proveedor)
        {
            if (id != proveedor.IdProveedor)
            {
                return NotFound();
            }

            var existing = await _context.Proveedor.FindAsync(id);
            if (existing is null) return NotFound();
            await _identificacion.ValidarProveedorAsync(proveedor, id, ModelState);
            if (ModelState.IsValid)
            {
                try
                {
                    existing.Nombre = proveedor.Nombre;
                    existing.Contacto = proveedor.Contacto;
                    existing.Ruc = proveedor.Ruc;
                    existing.Direccion = proveedor.Direccion;
                    existing.UbigeoCodigo = proveedor.UbigeoCodigo;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProveedorExists(proveedor.IdProveedor))
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

            var proveedor = await _context.Proveedor.Include(item => item.Ubigeo)
                .FirstOrDefaultAsync(m => m.IdProveedor == id);
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
            var proveedor = await _context.Proveedor.FindAsync(id);
            if (proveedor is null) return NotFound();
            if (await _context.Producto.AnyAsync(p => p.IdProveedor == id))
            {
                ModelState.AddModelError(string.Empty, "No se puede eliminar un proveedor con productos asociados.");
                return View("Delete", proveedor);
            }
            _context.Proveedor.Remove(proveedor);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProveedorExists(int id)
        {
            return _context.Proveedor.Any(e => e.IdProveedor == id);
        }
    }
}
