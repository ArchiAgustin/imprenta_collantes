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
    public class ClientesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IdentificacionService _identificacion;

        public ClientesController(ApplicationDbContext context, IdentificacionService identificacion)
        {
            _context = context;
            _identificacion = identificacion;
        }

        // GET: Clientes
        public async Task<IActionResult> Index()
        {
            return View(await _context.Cliente.AsNoTracking().Include(item => item.DocumentoIdentidadTipo).Include(item => item.Ubigeo).ToListAsync());
        }

        // GET: Clientes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cliente = await _context.Cliente.Include(item => item.DocumentoIdentidadTipo).Include(item => item.Ubigeo)
                .FirstOrDefaultAsync(m => m.IdCliente == id);
            if (cliente == null)
            {
                return NotFound();
            }

            return View(cliente);
        }

        // GET: Clientes/Create
        public async Task<IActionResult> Create()
        {
            await _identificacion.CargarCatalogosAsync(this, null);
            return View();
        }

        // POST: Clientes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Correo,Telefono,Direccion,IdDocumentoIdentidadTipo,NumeroDocumento,UbigeoCodigo")] Cliente cliente)
        {
            var (tipo, _) = await _identificacion.ValidarClienteAsync(cliente, null, ModelState);
            if (ModelState.IsValid)
            {
                if (tipo?.Codigo == "1") cliente.Dni = cliente.NumeroDocumento;
                _context.Add(cliente);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await _identificacion.CargarCatalogosAsync(this, cliente.UbigeoCodigo);
            return View(cliente);
        }

        // GET: Clientes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cliente = await _context.Cliente.FindAsync(id);
            if (cliente == null)
            {
                return NotFound();
            }
            await _identificacion.CargarCatalogosAsync(this, cliente.UbigeoCodigo);
            return View(cliente);
        }

        // POST: Clientes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdCliente,Nombre,Correo,Telefono,Direccion,IdDocumentoIdentidadTipo,NumeroDocumento,UbigeoCodigo")] Cliente cliente)
        {
            if (id != cliente.IdCliente)
            {
                return NotFound();
            }

            var existing = await _context.Cliente.FindAsync(id);
            if (existing is null) return NotFound();
            var (tipo, _) = await _identificacion.ValidarClienteAsync(cliente, id, ModelState);
            if (ModelState.IsValid)
            {
                try
                {
                    existing.Nombre = cliente.Nombre;
                    existing.Correo = cliente.Correo;
                    existing.Telefono = cliente.Telefono;
                    existing.Direccion = cliente.Direccion;
                    existing.IdDocumentoIdentidadTipo = cliente.IdDocumentoIdentidadTipo;
                    existing.NumeroDocumento = cliente.NumeroDocumento;
                    existing.UbigeoCodigo = cliente.UbigeoCodigo;
                    // The old DNI column is retained for legacy data; keep it consistent on edit.
                    existing.Dni = tipo?.Codigo == "1" ? cliente.NumeroDocumento : null;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClienteExists(cliente.IdCliente))
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
            await _identificacion.CargarCatalogosAsync(this, cliente.UbigeoCodigo);
            return View(cliente);
        }

        // GET: Clientes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cliente = await _context.Cliente.Include(item => item.DocumentoIdentidadTipo).Include(item => item.Ubigeo)
                .FirstOrDefaultAsync(m => m.IdCliente == id);
            if (cliente == null)
            {
                return NotFound();
            }

            return View(cliente);
        }

        // POST: Clientes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cliente = await _context.Cliente.FindAsync(id);
            if (cliente is null) return NotFound();
            if (await _context.Cotizacion.AnyAsync(c => c.IdCliente == id) ||
                await _context.Set<Pedido>().AnyAsync(p => p.IdCliente == id))
            {
                ModelState.AddModelError(string.Empty, "No se puede eliminar un cliente con cotizaciones o pedidos asociados.");
                return View("Delete", cliente);
            }
            _context.Cliente.Remove(cliente);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ClienteExists(int id)
        {
            return _context.Cliente.Any(e => e.IdCliente == id);
        }
    }
}
