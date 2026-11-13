namespace ProyectoFinal.Services;

public interface ICorreoComercialSender
{
    Task SendEmailWithAttachmentAsync(string email, string subject, string htmlMessage,
        byte[] attachmentContent, string attachmentName, string contentType);
}
