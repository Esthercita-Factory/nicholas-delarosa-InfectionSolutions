using InfectionSolutions.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace InfectionSolutions.Infrastructure.Email;

/// <summary>
/// Para desarrollo sin credenciales SMTP: el correo no sale, queda en el log.
/// Lanza para que el portal no diga "comprobante enviado" cuando no lo fue.
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Correo NO enviado (Email:Provider=Log o faltan credenciales SMTP). Para: {Para}. Asunto: {Asunto}. Adjuntos: {Adjuntos}",
            message.ToAddress,
            message.Subject,
            string.Join(", ", (message.Attachments ?? []).Select(a => a.FileName)));

        throw new InvalidOperationException("El envío de correos no está configurado (faltan las credenciales SMTP).");
    }
}
