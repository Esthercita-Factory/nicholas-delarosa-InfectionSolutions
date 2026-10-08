using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;

namespace InfectionSolutions.Application.Services.Sales;

public interface ISaleService
{
    Task<PagedResponse<SaleListItemResponse>> ListAsync(SaleQuery query, CancellationToken cancellationToken = default);

    Task<Result<SaleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registra la venta y descuenta el stock en una sola transaccion.</summary>
    Task<Result<SaleResponse>> CreateAsync(SaleRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Anula la venta y devuelve el stock de sus productos.</summary>
    Task<Result> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
