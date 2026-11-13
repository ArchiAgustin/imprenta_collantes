using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Models;
using ProyectoFinal.Services;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITransaccionIdentidadService _transactions;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ITransaccionIdentidadService transactions,
        IEmailSender emailSender,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _transactions = transactions;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users
            .OrderBy(user => user.Email)
            .ToListAsync();

        var model = new List<UsuarioAdminListViewModel>(users.Count);
        foreach (var user in users)
        {
            model.Add(new UsuarioAdminListViewModel
            {
                Id = user.Id,
                Correo = user.Email ?? user.UserName ?? "Sin correo",
                Roles = (await _userManager.GetRolesAsync(user)).ToList(),
                Activo = !IsDisabled(user),
                EsCuentaPropia = user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier),
                CorreoConfirmado = user.EmailConfirmed
            });
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult CrearUsuario() => View(new UsuarioCrearViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearUsuario(UsuarioCrearViewModel model)
    {
        model.Correo = model.Correo?.Trim() ?? string.Empty;
        if (await _userManager.FindByEmailAsync(model.Correo) is not null)
            ModelState.AddModelError(nameof(model.Correo), "Ya existe una cuenta con este correo.");
        if (!ModelState.IsValid) return View(model);

        var user = new IdentityUser
        {
            UserName = model.Correo,
            Email = model.Correo,
            EmailConfirmed = false
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        var sent = await SendConfirmationAsync(user);
        TempData["Success"] = sent
            ? $"Se creó la cuenta de {model.Correo} sin roles y se envió el enlace de confirmación. Asigna únicamente los accesos necesarios y entrega la clave por un canal seguro."
            : $"Se creó la cuenta de {model.Correo} sin roles, pero no se pudo enviar la confirmación. Configura SMTP y reenvía el enlace desde Usuarios.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReenviarConfirmacion(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();
        if (user.EmailConfirmed)
        {
            TempData["Error"] = "El correo de esta cuenta ya está confirmado.";
            return RedirectToAction(nameof(Index));
        }

        var sent = await SendConfirmationAsync(user);
        TempData[sent ? "Success" : "Error"] = sent
            ? $"Se envió un nuevo enlace de confirmación a {user.Email}."
            : "No se pudo enviar el enlace. Revisa la configuración SMTP y los registros de la aplicación.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(string userId)
    {
        await using var transaction = await _transactions.IniciarSerializableAsync();
        var target = await _userManager.FindByIdAsync(userId);
        if (target is null) return NotFound();
        if (target.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            TempData["Error"] = "No puedes desactivar tu propia cuenta.";
            return RedirectToAction(nameof(Index));
        }

        var activar = IsDisabled(target);
        var roles = await _userManager.GetRolesAsync(target);
        if (!activar && roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            var activeAdmins = (await _userManager.GetUsersInRoleAsync("Admin")).Count(user => !IsDisabled(user));
            if (activeAdmins <= 1)
            {
                TempData["Error"] = "Debe quedar al menos una cuenta Admin activa.";
                return RedirectToAction(nameof(Index));
            }
        }

        var lockoutEnabled = await _userManager.SetLockoutEnabledAsync(target, true);
        if (!lockoutEnabled.Succeeded)
        {
            AddIdentityErrors(lockoutEnabled);
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        var lockout = await _userManager.SetLockoutEndDateAsync(target,
            activar ? null : DateTimeOffset.MaxValue);
        if (!lockout.Succeeded)
        {
            TempData["Error"] = string.Join(" ", lockout.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }
        var stamp = await _userManager.UpdateSecurityStampAsync(target);
        if (!stamp.Succeeded)
        {
            TempData["Error"] = string.Join(" ", stamp.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }
        if (transaction is not null) await transaction.CommitAsync();

        TempData["Success"] = activar
            ? $"Se reactivó la cuenta {target.Email}."
            : $"Se desactivó {target.Email}; sus sesiones quedan invalidadas.";
        return RedirectToAction(nameof(Index));
    }

    // Legacy link retained as a safe redirect; role management now lives in its own module.
    [HttpGet]
    public IActionResult CreateRole() => RedirectToAction("Index", "Roles");

    [HttpGet]
    public async Task<IActionResult> AssignRole(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var model = new AssignRoleViewModel
        {
            UserId = user.Id,
            UserName = user.Email ?? user.UserName ?? user.Id,
            AvailableRoles = await GetRoleNamesAsync(),
            SelectedRoles = (await _userManager.GetRolesAsync(user)).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(AssignRoleViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null) return NotFound();

        var availableRoles = await GetRoleNamesAsync();
        var selectedRoles = (model.SelectedRoles ?? new List<string>())
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var invalidRole = selectedRoles.FirstOrDefault(role =>
            !availableRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
        if (invalidRole is not null)
            ModelState.AddModelError(nameof(model.SelectedRoles), "La selección contiene un rol que ya no existe.");

        await using var transaction = await _transactions.IniciarSerializableAsync();

        var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var removingAdmin = currentRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            && !selectedRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        var removingOwnAdmin = user.Id == currentUserId && removingAdmin;
        var preserveAdminAssignment = removingOwnAdmin;
        if (removingOwnAdmin)
            ModelState.AddModelError(nameof(model.SelectedRoles), "No puedes quitarte el rol Admin de tu propia cuenta; se mantuvo la asignación.");

        var remainingAdministrators = (await _userManager.GetUsersInRoleAsync("Admin"))
            .Where(candidate => !IsDisabled(candidate)).ToList();
        if (!removingOwnAdmin
            && removingAdmin
            && !IsDisabled(user)
            && remainingAdministrators.Count <= 1)
        {
            preserveAdminAssignment = true;
            ModelState.AddModelError(nameof(model.SelectedRoles), "Debe quedar al menos un administrador activo; se mantuvo la asignación.");
        }

        model.AvailableRoles = availableRoles;
        model.UserName = user.Email ?? user.UserName ?? user.Id;
        model.SelectedRoles = preserveAdminAssignment ? currentRoles : selectedRoles;
        if (!ModelState.IsValid) return View(model);

        var rolesToRemove = currentRoles
            .Where(role => !selectedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var rolesToAdd = selectedRoles
            .Where(role => !currentRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                if (transaction is not null) await transaction.RollbackAsync();
                AddIdentityErrors(removeResult);
                return View(model);
            }
        }

        if (rolesToAdd.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                if (transaction is not null) await transaction.RollbackAsync();
                AddIdentityErrors(addResult);
                return View(model);
            }
        }

        if (transaction is not null) await transaction.CommitAsync();
        TempData["Success"] = "Los roles del usuario se actualizaron.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> GetRoleNamesAsync() => await _roleManager.Roles
        .Where(role => role.Name != null)
        .OrderBy(role => role.Name)
        .Select(role => role.Name!)
        .ToListAsync();

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    private async Task<bool> SendConfirmationAsync(IdentityUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return false;
        try
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = encodedCode }, protocol: Request.Scheme);
            if (string.IsNullOrWhiteSpace(callbackUrl)) return false;
            await _emailSender.SendEmailAsync(user.Email, "Confirma tu correo · Imprenta Collantes",
                $"Confirma el correo de tu cuenta <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>desde este enlace</a>. Luego podrás recuperar tu contraseña por correo.");
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo enviar la confirmación de correo al usuario {UserId}", user.Id);
            return false;
        }
    }

    private static bool IsDisabled(IdentityUser user) =>
        user.LockoutEnd is { } until && until > DateTimeOffset.UtcNow.AddYears(50);
}
