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
    public class ClientesController : Controller
    {
        private readonly IClientesCatalogoService _catalogo;
        private readonly IdentificacionService _identificacion;
        private readonly FichaComercialService _fichaComercial;

        public ClientesController(IClientesCatalogoService catalogo, IdentificacionService identificacion, FichaComercialService fichaComercial)
        {
            _catalogo = catalogo;
            _identificacion = identificacion;
            _fichaComercial = fichaComercial;
        }

        // GET: Clientes
        public async Task<IActionResult> Index(string? nombre, string? documento, string? correo, string? telefono, int pagina = 1)
        {
            nombre = NormalizarFiltro(nombre);
            documento = NormalizarFiltro(documento);
            correo = NormalizarFiltro(correo);
            telefono = NormalizarFiltro(telefono);
            var result = await _catalogo.ListarAsync(nombre, documento, correo, telefono, pagina);
            ViewBag.NombreFiltro = nombre;
            ViewBag.DocumentoFiltro = documento;
            ViewBag.CorreoFiltro = correo;
            ViewBag.TelefonoFiltro = telefono;
            ViewBag.Pagina = result.Pagina;
            ViewBag.Paginas = result.Paginas;
            ViewBag.TotalClientes = result.Total;
            return View(result.Items);
        }

        // GET: Clientes/Details/5
        public async Task<IActionResult> Details(int? id, int paginaCotizaciones = 1, int paginaPedidos = 1)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ficha = await _fichaComercial.ObtenerAsync(id.Value, paginaCotizaciones, paginaPedidos);
            return ficha is null ? NotFound() : View(ficha);
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
                await _catalogo.CrearAsync(cliente, tipo?.Codigo == "1");
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

            var cliente = await _catalogo.ObtenerAsync(id.Value);
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

            var existing = await _catalogo.ObtenerAsync(id);
            if (existing is null) return NotFound();
            var (tipo, _) = await _identificacion.ValidarClienteAsync(cliente, id, ModelState);
            if (ModelState.IsValid)
            {
                try
                {
                    await _catalogo.ActualizarAsync(id, cliente, tipo?.Codigo == "1");
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
                {
                    if (!await _catalogo.ExisteAsync(cliente.IdCliente))
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

            var cliente = await _catalogo.ObtenerParaEliminarAsync(id.Value);
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
            var outcome = await _catalogo.EliminarAsync(id);
            if (outcome == ResultadoEliminarCatalogo.NoEncontrado) return NotFound();
            if (outcome == ResultadoEliminarCatalogo.EnUso)
            {
                ModelState.AddModelError(string.Empty, "No se puede eliminar un cliente con cotizaciones o pedidos asociados.");
                return View("Delete", await _catalogo.ObtenerParaEliminarAsync(id));
            }
            return RedirectToAction(nameof(Index));
        }

        private static string? NormalizarFiltro(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
