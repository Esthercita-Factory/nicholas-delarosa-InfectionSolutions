using InfectionSolutions.Application.Dtos.Sales;

namespace InfectionSolutions.Application.Dtos.Portal;

/// <summary>Compra desde el portal: el cliente sale del token, no de la peticion.</summary>
public sealed record CheckoutRequest
{
    public IReadOnlyList<SaleLineRequest> Lines { get; init; } = [];

    public string? Notes { get; init; }
}
