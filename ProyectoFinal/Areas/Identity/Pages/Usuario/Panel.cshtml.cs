using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ProyectoFinal.Pages
{
    [Authorize(Roles = "Admin")]
    public class PanelModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
