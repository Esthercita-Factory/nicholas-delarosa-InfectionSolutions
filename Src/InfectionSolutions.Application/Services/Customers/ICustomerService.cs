using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;

namespace InfectionSolutions.Application.Services.Customers;

public interface ICustomerService
{
    Task<PagedResponse<CustomerResponse>> ListAsync(CustomerQuery query, CancellationToken cancellationToken = default);

    Task<Result<CustomerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Clientes activos para el formulario de ventas.</summary>
    Task<IReadOnlyList<CustomerOption>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<Result<CustomerResponse>> CreateAsync(CustomerRequest request, CancellationToken cancellationToken = default);

    Task<Result<CustomerResponse>> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Falla si el cliente tiene ventas: en ese caso se desactiva en lugar de borrarlo.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
