using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;

namespace InfectionSolutions.Application.Services.Sales;

public interface ISaleService
{
    Task<PagedResponse<SaleListItemResponse>> ListAsync(SaleQuery query, CancellationToken cancellationToken = default);

    /// <summary>Todas las que cumplen el filtro, sin paginar (exportaciones).</summary>
    Task<IReadOnlyList<SaleListItemResponse>> ListAllAsync(SaleQuery query, CancellationToken cancellationToken = default);

    Task<Result<SaleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registra la venta y descuenta el stock en una sola transaccion; despues genera el recibo PDF.</summary>
    Task<Result<SaleResponse>> CreateAsync(SaleRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Anula la venta, devuelve el stock de sus productos y regenera el recibo como ANULADA.</summary>
    Task<Result> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
