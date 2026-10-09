using InfectionSolutions.Application.Dtos.Sales;

namespace InfectionSolutions.Application.Services.Notifications;

/// <summary>
/// Correos al cliente. Nunca lanzan: un correo que no sale no deshace el
/// registro ni la compra; devuelven si se envio para avisarlo en pantalla.
/// </summary>
public interface ICustomerNotifier
{
    Task<bool> SendWelcomeAsync(string email, string fullName, CancellationToken cancellationToken = default);

    /// <summary>Confirmacion de compra con el recibo PDF adjunto.</summary>
    Task<bool> SendPurchaseConfirmationAsync(SaleResponse sale, CancellationToken cancellationToken = default);
}
