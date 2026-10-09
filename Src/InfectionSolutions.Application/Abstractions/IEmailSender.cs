namespace InfectionSolutions.Application.Abstractions;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>
/// Envio de correos. Hoy lo implementa un cliente SMTP configurado para Gmail;
/// pasar a un SMTP empresarial (u otro proveedor) es cambiar la configuracion o
/// registrar otra implementacion, sin tocar los casos de uso.
/// </summary>
public interface IEmailSender
{
    /// <summary>Lanza excepcion si el servidor rechaza el mensaje: quien llama decide si es grave.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
