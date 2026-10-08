using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Abstractions;

public sealed record CustomerFilter(string? Term, bool OnlyActive);

public interface ICustomerRepository
{
    /// <summary>Term busca por nombre o documento.</summary>
    Task<PagedResult<Customer>> ListAsync(CustomerFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> DocumentExistsAsync(string document, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> HasSalesAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    void Remove(Customer customer);
}
