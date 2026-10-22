using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoFinal.Areas.Identity.Pages.Account.Manage;

[Authorize(Roles = "Admin")]
public class EditRoleModel : PageModel
{
    [BindProperty]
    public IdentityRole Role { get; set; } = new();

    public IActionResult OnGet(string id) => RedirectToAction("Edit", "Roles", new { id });

    public IActionResult OnPost() => RedirectToAction("Index", "Roles");
}
