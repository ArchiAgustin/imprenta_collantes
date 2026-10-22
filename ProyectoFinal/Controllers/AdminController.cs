using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Models;

namespace ProyectoFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // Lista de usuarios
        public IActionResult Index()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        // Vista para crear un rol
        public IActionResult CreateRole() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            if (!string.IsNullOrWhiteSpace(roleName) && !await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "El nombre del rol es inválido o ya existe.");
            return View();
        }

        // Vista para asignar rol
        public async Task<IActionResult> AssignRole(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return NotFound();
            var roles = _roleManager.Roles.ToList();

            var model = new AssignRoleViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? user.Email ?? user.Id,
                AvailableRoles = roles.Select(r => r.Name).Where(name => name is not null).Select(name => name!).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(AssignRoleViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);

            if (user != null && !string.IsNullOrWhiteSpace(model.SelectedRole) && await _roleManager.RoleExistsAsync(model.SelectedRole))
            {
                if (await _userManager.IsInRoleAsync(user, model.SelectedRole))
                    return RedirectToAction(nameof(Index));
                var result = await _userManager.AddToRoleAsync(user, model.SelectedRole);
                if (result.Succeeded) return RedirectToAction(nameof(Index));
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            }
            else ModelState.AddModelError("", "Usuario o rol inválido.");
            model.AvailableRoles = _roleManager.Roles.Select(role => role.Name!).ToList();
            return View(model);
        }
    }
}
