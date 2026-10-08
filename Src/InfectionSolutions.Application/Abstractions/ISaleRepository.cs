using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Application.Abstractions;

public sealed record SaleFilter(string? Term, SaleStatus? Status);

public interface ISaleRepository
{
    /// <summary>Term busca por numero de venta, nombre o documento del cliente. Incluye el cliente.</summary>
    Task<PagedResult<Sale>> ListAsync(SaleFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Incluye cliente y detalles con su producto.</summary>
    Task<Sale?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountByNumberPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task AddAsync(Sale sale, CancellationToken cancellationToken = default);
}
