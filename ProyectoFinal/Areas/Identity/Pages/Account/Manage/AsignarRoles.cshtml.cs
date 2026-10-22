using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoFinal.Areas.Identity.Pages.Account.Manage;

[Authorize(Roles = "Admin")]
public class AsignarRolesModel : PageModel
{
    public List<IdentityUser> Users { get; set; } = new();
    public List<string> AllRoles { get; set; } = new();
    public Dictionary<string, IList<string>> UserRoles { get; set; } = new();

    public IActionResult OnGet() => RedirectToAction("Index", "Admin");

    public IActionResult OnPost(string userId) =>
        RedirectToAction("AssignRole", "Admin", new { userId });
}
