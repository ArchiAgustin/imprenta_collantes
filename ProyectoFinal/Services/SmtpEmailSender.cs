using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;

namespace ProyectoFinal.Services;

public sealed class SmtpEmailSender(
    ApplicationDbContext db,
    IDataProtectionProvider protectionProvider,
    ILogger<SmtpEmailSender> logger) : IEmailSender, ICorreoComercialSender
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("ProyectoFinal.SmtpSettings.v1");

    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        SendCoreAsync(email, subject, htmlMessage, null, null, null);

    public Task SendEmailWithAttachmentAsync(string email, string subject, string htmlMessage,
        byte[] attachmentContent, string attachmentName, string contentType) =>
        SendCoreAsync(email, subject, htmlMessage, attachmentContent, attachmentName, contentType);

    private async Task SendCoreAsync(string email, string subject, string htmlMessage,
        byte[]? attachmentContent, string? attachmentName, string? contentType)
    {
        var settings = await db.ConfiguracionCorreo.AsNoTracking().SingleOrDefaultAsync(item => item.Id == 1);
        if (settings is null)
            throw new InvalidOperationException("El correo SMTP no está configurado. Un administrador debe completar Configuración > Parámetros.");

        var password = string.IsNullOrEmpty(settings.ContrasenaProtegida)
            ? null
            : _protector.Unprotect(settings.ContrasenaProtegida);
        using var message = new MailMessage
        {
            From = new MailAddress(settings.CorreoRemitente, settings.NombreRemitente),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));
        if (attachmentContent is not null)
        {
            var attachment = new Attachment(new MemoryStream(attachmentContent), attachmentName ?? "documento.pdf",
                contentType ?? "application/pdf");
            message.Attachments.Add(attachment);
        }
        using var client = new SmtpClient(settings.Servidor, settings.Puerto)
        {
            EnableSsl = settings.UsarTls,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(settings.Usuario)
                ? null
                : new NetworkCredential(settings.Usuario, password),
            Timeout = 10_000
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or CryptographicException)
        {
            logger.LogError(exception, "No se pudo enviar correo SMTP. Destinatario: {Recipient}", email);
            throw;
        }
    }
}
