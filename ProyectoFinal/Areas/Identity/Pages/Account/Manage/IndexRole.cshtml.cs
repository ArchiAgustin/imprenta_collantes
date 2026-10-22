using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoFinal.Areas.Identity.Pages.Account.Manage;

[Authorize(Roles = "Admin")]
public class IndexModelRole : PageModel
{
    [BindProperty]
    public string RoleName { get; set; } = string.Empty;

    public IList<IdentityRole> Roles { get; set; } = new List<IdentityRole>();

    public IActionResult OnGet() => RedirectToAction("Index", "Roles");

    public IActionResult OnPost() => RedirectToAction("Index", "Roles");

    public IActionResult OnPostDelete(string id) => RedirectToAction("Index", "Roles");
}
