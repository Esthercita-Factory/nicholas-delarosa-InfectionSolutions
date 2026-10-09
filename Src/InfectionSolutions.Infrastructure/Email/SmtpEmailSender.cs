using InfectionSolutions.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InfectionSolutions.Infrastructure.Email;

/// <summary>Envio por SMTP con MailKit (System.Net.Mail.SmtpClient esta marcado como obsoleto para uso nuevo).</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = _options.Smtp;
        var remitente = string.IsNullOrWhiteSpace(_options.FromAddress) ? smtp.UserName! : _options.FromAddress;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, remitente));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;

        var cuerpo = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody };
        foreach (var adjunto in message.Attachments ?? [])
        {
            cuerpo.Attachments.Add(adjunto.FileName, adjunto.Content, ContentType.Parse(adjunto.ContentType));
        }

        mime.Body = cuerpo.ToMessageBody();

        using var cliente = new SmtpClient { Timeout = smtp.TimeoutSeconds * 1000 };
        await cliente.ConnectAsync(smtp.Host, smtp.Port, Seguridad(smtp.Security), cancellationToken);
        // Solo se registra este emisor con credenciales (EmailOptions.SmtpConfigured).
        await cliente.AuthenticateAsync(smtp.UserName!, smtp.Password!, cancellationToken);
        await cliente.SendAsync(mime, cancellationToken);
        await cliente.DisconnectAsync(true, cancellationToken);
    }

    private static SecureSocketOptions Seguridad(string valor) => valor.Trim().ToLowerInvariant() switch
    {
        "sslonconnect" or "ssl" => SecureSocketOptions.SslOnConnect,
        "none" => SecureSocketOptions.None,
        "auto" => SecureSocketOptions.Auto,
        _ => SecureSocketOptions.StartTls
    };
}
