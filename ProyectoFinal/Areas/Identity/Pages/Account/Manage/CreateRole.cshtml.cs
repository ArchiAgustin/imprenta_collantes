using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoFinal.Areas.Identity.Pages.Account.Manage;

[Authorize(Roles = "Admin")]
public class CreateRoleModel : PageModel
{
    [BindProperty]
    public string RoleName { get; set; } = string.Empty;

    public IActionResult OnGet() => RedirectToAction("Index", "Roles");

    public IActionResult OnPost() => RedirectToAction("Index", "Roles");
}
