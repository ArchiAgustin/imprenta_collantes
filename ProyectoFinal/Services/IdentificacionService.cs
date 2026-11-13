using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class IdentificacionService(ApplicationDbContext db) : IUbigeoConsultaService
{
    public async Task<IReadOnlyList<UbigeoHijo>?> HijosAsync(string parent)
    {
        if (!await db.Ubigeos.AnyAsync(item => item.Codigo == parent)) return null;
        return await db.Ubigeos.AsNoTracking().Where(item => item.Parent == parent)
            .OrderBy(item => item.Descripcion)
            .Select(item => new UbigeoHijo(item.Codigo, item.Descripcion)).ToListAsync();
    }
    public async Task<(DocumentoIdentidadTipo? Tipo, string? Numero)> ValidarClienteAsync(Cliente cliente, int? excluirId, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary errors)
    {
        cliente.Nombre = cliente.Nombre?.Trim();
        cliente.Correo = string.IsNullOrWhiteSpace(cliente.Correo) ? null : cliente.Correo.Trim();
        cliente.Telefono = string.IsNullOrWhiteSpace(cliente.Telefono) ? null : cliente.Telefono.Trim();
        cliente.Direccion = cliente.Direccion?.Trim();
        cliente.NumeroDocumento = cliente.NumeroDocumento?.Trim();
        cliente.UbigeoCodigo = string.IsNullOrWhiteSpace(cliente.UbigeoCodigo) ? null : cliente.UbigeoCodigo.Trim();

        if (string.IsNullOrWhiteSpace(cliente.Nombre))
            errors.AddModelError(nameof(cliente.Nombre), "Ingresa el nombre o la razón social.");
        var tipo = await db.DocumentoIdentidadTipos.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdDocumentoIdentidadTipo == cliente.IdDocumentoIdentidadTipo
                && item.Estado && !item.Eliminado && (item.Codigo == "1" || item.Codigo == "6"));
        if (tipo is null)
            errors.AddModelError(nameof(cliente.IdDocumentoIdentidadTipo), "Selecciona DNI o RUC.");
        else if (cliente.NumeroDocumento is null || !Regex.IsMatch(cliente.NumeroDocumento,
            tipo.Codigo == "1" ? @"^[0-9]{8}$" : @"^[0-9]{11}$", RegexOptions.CultureInvariant))
            errors.AddModelError(nameof(cliente.NumeroDocumento), $"Ingresa un {(tipo.Codigo == "1" ? "DNI de 8" : "RUC de 11")} dígitos.");
        else if (await db.Cliente.AnyAsync(item => item.IdDocumentoIdentidadTipo == tipo.IdDocumentoIdentidadTipo
            && item.NumeroDocumento == cliente.NumeroDocumento && item.IdCliente != excluirId))
            errors.AddModelError(nameof(cliente.NumeroDocumento), "Ya existe un cliente con este documento.");

        if (tipo?.Codigo == "6" && string.IsNullOrWhiteSpace(cliente.Direccion))
            errors.AddModelError(nameof(cliente.Direccion), "La dirección es obligatoria para clientes con RUC.");
        await ValidarUbigeoAsync(cliente.UbigeoCodigo, tipo?.Codigo == "6", errors);
        return (tipo, cliente.NumeroDocumento);
    }

    public async Task ValidarProveedorAsync(Proveedor proveedor, int? excluirId, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary errors)
    {
        proveedor.Nombre = proveedor.Nombre?.Trim();
        proveedor.Direccion = proveedor.Direccion?.Trim();
        proveedor.NumeroDocumento = proveedor.NumeroDocumento?.Trim();
        proveedor.Ruc = proveedor.Ruc?.Trim();
        proveedor.UbigeoCodigo = string.IsNullOrWhiteSpace(proveedor.UbigeoCodigo) ? null : proveedor.UbigeoCodigo.Trim();
        if (string.IsNullOrWhiteSpace(proveedor.Nombre))
            errors.AddModelError(nameof(proveedor.Nombre), "Ingresa el nombre del proveedor.");
        // Accept legacy posts/integrations that still submit only Ruc.
        if (proveedor.IdDocumentoIdentidadTipo is null && !string.IsNullOrWhiteSpace(proveedor.Ruc))
        {
            proveedor.NumeroDocumento = proveedor.Ruc;
            proveedor.IdDocumentoIdentidadTipo = await db.DocumentoIdentidadTipos.AsNoTracking()
                .Where(item => item.Codigo == "6" && item.Estado && !item.Eliminado)
                .Select(item => (int?)item.IdDocumentoIdentidadTipo).FirstOrDefaultAsync();
        }
        var tipo = await db.DocumentoIdentidadTipos.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdDocumentoIdentidadTipo == proveedor.IdDocumentoIdentidadTipo
                && item.Estado && !item.Eliminado && (item.Codigo == "1" || item.Codigo == "4" || item.Codigo == "6"));
        if (tipo is null)
            errors.AddModelError(nameof(proveedor.NumeroDocumento), "Selecciona un tipo de documento e ingresa un RUC de 11 dígitos.");
        else
        {
            var longitud = tipo.Codigo switch { "1" => 8, "4" => 9, _ => 11 };
            if (proveedor.NumeroDocumento is null || !Regex.IsMatch(proveedor.NumeroDocumento, $@"^[0-9]{{{longitud}}}$", RegexOptions.CultureInvariant))
                errors.AddModelError(nameof(proveedor.NumeroDocumento), tipo.Codigo == "6" ? "Ingresa un RUC de 11 dígitos." : $"Ingresa un documento de {longitud} dígitos.");
            else if (await db.Proveedor.AnyAsync(item => item.IdDocumentoIdentidadTipo == tipo.IdDocumentoIdentidadTipo
                && item.NumeroDocumento == proveedor.NumeroDocumento && item.IdProveedor != excluirId))
                errors.AddModelError(nameof(proveedor.NumeroDocumento), "Ya existe un proveedor con este documento.");
            proveedor.Ruc = tipo.Codigo == "6" ? proveedor.NumeroDocumento : null;
        }
        if (string.IsNullOrWhiteSpace(proveedor.Direccion))
            errors.AddModelError(nameof(proveedor.Direccion), "Ingresa la dirección del proveedor.");
        await ValidarUbigeoAsync(proveedor.UbigeoCodigo, true, errors);
    }

    private async Task ValidarUbigeoAsync(string? codigo, bool obligatorio, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary errors)
    {
        if (codigo is null)
        {
            if (obligatorio) errors.AddModelError("UbigeoCodigo", "Selecciona departamento, provincia y distrito.");
            return;
        }
        var item = await db.Ubigeos.AsNoTracking().FirstOrDefaultAsync(location => location.Codigo == codigo);
        if (item is null || item.IdParent is null or 0
            || !await db.Ubigeos.AnyAsync(parent => parent.IdUbigeo == item.IdParent && parent.IdParent != null && parent.IdParent != 0))
            errors.AddModelError("UbigeoCodigo", "Selecciona un distrito válido del catálogo.");
    }

    public async Task CargarCatalogosAsync(Microsoft.AspNetCore.Mvc.Controller controller, string? codigo)
    {
        controller.ViewBag.TiposDocumento = await db.DocumentoIdentidadTipos.AsNoTracking()
            .Where(item => item.Estado && !item.Eliminado && (item.Codigo == "1" || item.Codigo == "4" || item.Codigo == "6"))
            .OrderBy(item => item.Codigo).ToListAsync();

        var seleccionado = codigo is null ? null : await db.Ubigeos.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Codigo == codigo);
        var provincia = seleccionado is null ? null : await db.Ubigeos.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdUbigeo == seleccionado.IdParent);
        var departamento = provincia is null ? null : await db.Ubigeos.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdUbigeo == provincia.IdParent);
        controller.ViewBag.Departamentos = await db.Ubigeos.AsNoTracking()
            .Where(item => item.IdParent == 0).OrderBy(item => item.Descripcion).ToListAsync();
        controller.ViewBag.Provincias = departamento is null ? new List<Ubigeo>() : await db.Ubigeos.AsNoTracking()
            .Where(item => item.IdParent == departamento.IdUbigeo).OrderBy(item => item.Descripcion).ToListAsync();
        controller.ViewBag.Distritos = provincia is null ? new List<Ubigeo>() : await db.Ubigeos.AsNoTracking()
            .Where(item => item.IdParent == provincia.IdUbigeo).OrderBy(item => item.Descripcion).ToListAsync();
        controller.ViewBag.DepartamentoSeleccionado = departamento?.Codigo;
        controller.ViewBag.ProvinciaSeleccionada = provincia?.Codigo;
        controller.ViewBag.DistritoSeleccionado = seleccionado?.Codigo;
    }
}
