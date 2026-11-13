using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ParametrosController(
    IParametrosCorreoService settingsService,
    IEmailSender emailSender,
    UserManager<IdentityUser> userManager,
    ILogger<ParametrosController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await settingsService.ObtenerAsync();
        await SetTestAddressAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(ParametrosCorreoViewModel model)
    {
        Normalize(model);
        if (string.IsNullOrWhiteSpace(model.Usuario) && !string.IsNullOrWhiteSpace(model.Contrasena))
            ModelState.AddModelError(nameof(model.Contrasena), "Ingresa un usuario SMTP para guardar una contraseña.");

        if (!ModelState.IsValid)
        {
            model.TieneContrasenaGuardada = await settingsService.TieneContrasenaAsync();
            await SetTestAddressAsync(model);
            return View("Index", model);
        }

        await settingsService.GuardarAsync(model);
        TempData["Success"] = "Se guardaron los parámetros SMTP. Envía un correo de prueba para comprobar la conexión.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarPrueba()
    {
        var user = await userManager.GetUserAsync(User);
        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            TempData["Error"] = "La cuenta Admin no tiene un correo registrado para recibir la prueba.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await emailSender.SendEmailAsync(user.Email, "Prueba de correo · Imprenta Collantes",
                "La configuración SMTP de Imprenta Collantes funciona correctamente.");
            TempData["Success"] = $"Se envió un correo de prueba a {user.Email}.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falló el correo de prueba SMTP para el administrador {AdminId}", user?.Id);
            TempData["Error"] = "No se pudo enviar la prueba. Revisa el servidor, puerto, TLS, credenciales y los registros de la aplicación.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task SetTestAddressAsync(ParametrosCorreoViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        model.UsuarioPrueba = user?.Email;
    }

    private static void Normalize(ParametrosCorreoViewModel model)
    {
        model.Servidor = model.Servidor?.Trim() ?? string.Empty;
        model.CorreoRemitente = model.CorreoRemitente?.Trim() ?? string.Empty;
        model.NombreRemitente = model.NombreRemitente?.Trim() ?? string.Empty;
        model.Usuario = string.IsNullOrWhiteSpace(model.Usuario) ? null : model.Usuario.Trim();
    }
}
