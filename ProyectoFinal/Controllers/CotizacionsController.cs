using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CotizacionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CotizacionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Cotizacions
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Cotizacion.Include(c => c.IdClienteNavigation).Include(c => c.IdUsuarioNavigation);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Cotizacions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cotizacion = await _context.Cotizacion
                .Include(c => c.IdClienteNavigation)
                .Include(c => c.IdUsuarioNavigation)
                .FirstOrDefaultAsync(m => m.IdCotizacion == id);
            if (cotizacion == null)
            {
                return NotFound();
            }

            return View(cotizacion);
        }

        // GET: Cotizacions/Create
        public IActionResult Create()
        {
            ViewData["IdCliente"] = new SelectList(_context.Cliente, "IdCliente", "Nombre");
            return View();
        }

        // POST: Cotizacions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdCliente,FechaVencimiento,DetalleCotizacion,Total")] Cotizacion cotizacion)
        {
            cotizacion.IdUsuario = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            cotizacion.FechaCreacion = DateOnly.FromDateTime(DateTime.UtcNow);
            if (cotizacion.IdCliente is null || !await _context.Cliente.AnyAsync(c => c.IdCliente == cotizacion.IdCliente))
                ModelState.AddModelError(nameof(Cotizacion.IdCliente), "Selecciona un cliente registrado.");
            if (cotizacion.Total is < 0)
                ModelState.AddModelError(nameof(Cotizacion.Total), "El importe no puede ser negativo.");
            if (ModelState.IsValid)
            {
                _context.Add(cotizacion);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdCliente"] = new SelectList(_context.Cliente, "IdCliente", "Nombre", cotizacion.IdCliente);
            return View(cotizacion);
        }

        // GET: Cotizacions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cotizacion = await _context.Cotizacion.FindAsync(id);
            if (cotizacion == null)
            {
                return NotFound();
            }
            ViewData["IdCliente"] = new SelectList(_context.Cliente, "IdCliente", "Nombre", cotizacion.IdCliente);
            return View(cotizacion);
        }

        // POST: Cotizacions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdCotizacion,IdCliente,FechaVencimiento,DetalleCotizacion,Total")] Cotizacion cotizacion)
        {
            if (id != cotizacion.IdCotizacion)
            {
                return NotFound();
            }

            if (cotizacion.IdCliente is null || !await _context.Cliente.AnyAsync(c => c.IdCliente == cotizacion.IdCliente))
                ModelState.AddModelError(nameof(Cotizacion.IdCliente), "Selecciona un cliente registrado.");
            if (cotizacion.Total is < 0)
                ModelState.AddModelError(nameof(Cotizacion.Total), "El importe no puede ser negativo.");
            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Cotizacion.FindAsync(id);
                    if (existing is null) return NotFound();
                    existing.IdCliente = cotizacion.IdCliente;
                    existing.FechaVencimiento = cotizacion.FechaVencimiento;
                    existing.DetalleCotizacion = cotizacion.DetalleCotizacion;
                    existing.Total = cotizacion.Total;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CotizacionExists(cotizacion.IdCotizacion))
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
            ViewData["IdCliente"] = new SelectList(_context.Cliente, "IdCliente", "Nombre", cotizacion.IdCliente);
            return View(cotizacion);
        }

        // GET: Cotizacions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cotizacion = await _context.Cotizacion
                .Include(c => c.IdClienteNavigation)
                .Include(c => c.IdUsuarioNavigation)
                .FirstOrDefaultAsync(m => m.IdCotizacion == id);
            if (cotizacion == null)
            {
                return NotFound();
            }

            return View(cotizacion);
        }

        // POST: Cotizacions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cotizacion = await _context.Cotizacion.FindAsync(id);
            if (cotizacion is null) return NotFound();
            if (await _context.Set<Pedido>().AnyAsync(p => p.IdCotizacion == id) ||
                await _context.Set<DetalleCotizacion>().AnyAsync(d => d.IdCotizacion == id))
            {
                await _context.Entry(cotizacion).Reference(c => c.IdClienteNavigation).LoadAsync();
                await _context.Entry(cotizacion).Reference(c => c.IdUsuarioNavigation).LoadAsync();
                ModelState.AddModelError(string.Empty, "No se puede eliminar una cotización con detalles o pedidos asociados.");
                return View("Delete", cotizacion);
            }
            _context.Cotizacion.Remove(cotizacion);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CotizacionExists(int id)
        {
            return _context.Cotizacion.Any(e => e.IdCotizacion == id);
        }
    }
}
