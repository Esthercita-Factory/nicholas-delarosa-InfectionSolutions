using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Abstractions;

/// <param name="Active">null = todos, true = solo activos, false = solo inactivos.</param>
public sealed record CustomerFilter(string? Term, bool? Active);

public interface ICustomerRepository
{
    /// <summary>Term busca por nombre o documento.</summary>
    Task<PagedResult<Customer>> ListAsync(CustomerFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Todos los que cumplen el filtro, sin paginar (exportaciones).</summary>
    Task<IReadOnlyList<Customer>> ListAllAsync(CustomerFilter filter, CancellationToken cancellationToken = default);

    Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Rastreados, para actualizarlos en la carga masiva. Los documentos ya vienen normalizados.</summary>
    Task<IReadOnlyList<Customer>> FindByDocumentsAsync(IReadOnlyCollection<string> documents, CancellationToken cancellationToken = default);

    /// <summary>Clientes que ya usan alguno de esos correos (en minusculas).</summary>
    Task<IReadOnlyList<Customer>> FindByEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> DocumentExistsAsync(string document, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> HasSalesAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    void Remove(Customer customer);
}
