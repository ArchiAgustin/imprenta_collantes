using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;

namespace ProyectoFinal.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public AdminController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
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
                Roles = (await _userManager.GetRolesAsync(user)).ToList()
            });
        }

        return View(model);
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

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var removingAdmin = currentRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            && !selectedRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        var removingOwnAdmin = user.Id == currentUserId && removingAdmin;
        var preserveAdminAssignment = removingOwnAdmin;
        if (removingOwnAdmin)
            ModelState.AddModelError(nameof(model.SelectedRoles), "No puedes quitarte el rol Admin de tu propia cuenta; se mantuvo la asignación.");

        var remainingAdministrators = await _userManager.GetUsersInRoleAsync("Admin");
        if (!removingOwnAdmin
            && removingAdmin
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
}
