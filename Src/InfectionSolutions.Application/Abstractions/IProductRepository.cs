using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Abstractions;

public sealed record ProductFilter(string? Term, string? Category, bool OnlyActive);

public interface IProductRepository
{
    /// <summary>Term busca por nombre, codigo o categoria.</summary>
    Task<PagedResult<Product>> ListAsync(ProductFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<Product?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Bloquea las filas con FOR UPDATE; usar dentro de una transaccion.</summary>
    Task<IReadOnlyList<Product>> FindByIdsForUpdateAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(string sku, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> HasSalesAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    void Remove(Product product);
}
