using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProyectoFinal.Services;

namespace ProyectoFinal.Pages
{
    [Authorize]
    public class PanelModel(IMenuAccessService menuAccess) : PageModel
    {
        public IReadOnlyList<MenuNavigationItem> Menus { get; private set; } = Array.Empty<MenuNavigationItem>();

        public async Task OnGetAsync()
        {
            Menus = await menuAccess.GetNavigationMenusAsync(User);
        }
    }
}
