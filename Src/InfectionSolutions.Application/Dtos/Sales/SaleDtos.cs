using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Application.Dtos.Sales;

public sealed record SaleLineRequest(Guid ProductId, int Quantity);

public sealed record SaleRequest
{
    public Guid CustomerId { get; init; }

    public IReadOnlyList<SaleLineRequest> Lines { get; init; } = [];

    public string? Notes { get; init; }
}

public sealed record SaleListItemResponse(
    Guid Id,
    string SaleNumber,
    DateTimeOffset SaleDate,
    string CustomerName,
    string CustomerDocument,
    int ItemCount,
    decimal Total,
    SaleStatus Status);

public sealed record SaleDetailResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record SaleResponse(
    Guid Id,
    string SaleNumber,
    DateTimeOffset SaleDate,
    SaleStatus Status,
    Guid CustomerId,
    string CustomerName,
    string CustomerDocument,
    string CustomerEmail,
    string CustomerPhone,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string? Notes,
    DateTimeOffset? CancelledAt,
    IReadOnlyList<SaleDetailResponse> Details);

public sealed record SaleQuery
{
    /// <summary>Busca por numero de venta, nombre o documento del cliente.</summary>
    public string? Q { get; init; }

    public SaleStatus? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
