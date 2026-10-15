using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Receipts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfectionSolutions.Application.Services.Notifications;

public sealed class CustomerNotifier : ICustomerNotifier
{
    private readonly IEmailSender _email;
    private readonly IReceiptService _receipts;
    private readonly CompanyOptions _company;
    private readonly ILogger<CustomerNotifier> _logger;

    public CustomerNotifier(
        IEmailSender email,
        IReceiptService receipts,
        IOptions<CompanyOptions> company,
        ILogger<CustomerNotifier> logger)
    {
        _email = email;
        _receipts = receipts;
        _company = company.Value;
        _logger = logger;
    }

    public async Task<bool> SendWelcomeAsync(string email, string fullName, CancellationToken cancellationToken = default)
    {
        var contenido = EmailTemplates.Welcome(fullName, _company);

        return await EnviarAsync(new EmailMessage(email, fullName, contenido.Subject, contenido.Html, contenido.Text), "bienvenida", cancellationToken);
    }

    public async Task<bool> SendPurchaseConfirmationAsync(SaleResponse sale, CancellationToken cancellationToken = default)
    {
        // El recibo ya quedo guardado al crear la venta; si fallo, GetAsync lo genera.
        var recibo = await _receipts.GetAsync(sale.Id, cancellationToken);
        if (recibo.IsFailure)
        {
            _logger.LogWarning("No se encontró el recibo de la venta {SaleNumber}: {Error}", sale.SaleNumber, recibo.Error!.Message);
            return false;
        }

        var contenido = EmailTemplates.PurchaseConfirmation(sale, _company);
        var adjunto = new EmailAttachment(recibo.Value!.FileName, ReceiptFile.ContentType, recibo.Value.Content);
        var mensaje = new EmailMessage(sale.CustomerEmail, sale.CustomerName, contenido.Subject, contenido.Html, contenido.Text, [adjunto]);

        return await EnviarAsync(mensaje, $"comprobante {sale.SaleNumber}", cancellationToken);
    }

    public async Task<bool> SendPasswordResetAsync(string email, string fullName, string resetLink, CancellationToken cancellationToken = default)
    {
        var contenido = EmailTemplates.PasswordReset(fullName, resetLink, _company);

        return await EnviarAsync(new EmailMessage(email, fullName, contenido.Subject, contenido.Html, contenido.Text), "recuperación de contraseña", cancellationToken);
    }

    private async Task<bool> EnviarAsync(EmailMessage mensaje, string motivo, CancellationToken cancellationToken)
    {
        try
        {
            await _email.SendAsync(mensaje, cancellationToken);
            _logger.LogInformation("Correo de {Motivo} enviado a {Correo}", motivo, mensaje.ToAddress);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "No se pudo enviar el correo de {Motivo} a {Correo}", motivo, mensaje.ToAddress);
            return false;
        }
    }
}
