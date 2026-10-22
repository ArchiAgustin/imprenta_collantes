namespace ProyectoFinal.Models
{
    public class AssignRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<string> AvailableRoles { get; set; } = new();
        public string SelectedRole { get; set; } = string.Empty;
    }
}
