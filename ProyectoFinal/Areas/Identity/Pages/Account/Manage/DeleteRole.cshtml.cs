using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoFinal.Areas.Identity.Pages.Account.Manage;

[Authorize(Roles = "Admin")]
public class DeleteRoleModel : PageModel
{
    public IActionResult OnGet() => RedirectToAction("Index", "Roles");

    public IActionResult OnPost(string id) => RedirectToAction("Index", "Roles");
}
