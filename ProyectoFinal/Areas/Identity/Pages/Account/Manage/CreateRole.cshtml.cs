using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProyectoFinal.Data;
using System.Threading.Tasks;

namespace ProyectoFinal.Areas.Identity.Pages.Account.Manage
{
    [Authorize(Roles = "Admin")] // Solo los administradores pueden acceder
    public class CreateRoleModel : PageModel
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<CreateRoleModel> _logger;

        public CreateRoleModel(RoleManager<IdentityRole> roleManager, ILogger<CreateRoleModel> logger)
        {
            _roleManager = roleManager;
            _logger = logger;
        }

        [BindProperty]
        public string RoleName { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(RoleName))
            {
                ModelState.AddModelError(string.Empty, "Role name is required.");
                return Page();
            }

            var role = new IdentityRole { Name = RoleName };
            var result = await _roleManager.CreateAsync(role);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Role {RoleName} created successfully.");
                return RedirectToPage("./IndexRole"); // Redirigir a la página de roles después de crear el rol
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
