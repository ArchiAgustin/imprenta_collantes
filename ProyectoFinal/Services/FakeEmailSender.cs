namespace ProyectoFinal.Services
{
    using Microsoft.AspNetCore.Identity.UI.Services;
    using System.Threading.Tasks;

    public class FakeEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Simular envío de correo (puedes registrar logs si quieres)
            Console.WriteLine($"Simulando envío de correo a {email}: {subject}");
            return Task.CompletedTask;
        }
    }

}
