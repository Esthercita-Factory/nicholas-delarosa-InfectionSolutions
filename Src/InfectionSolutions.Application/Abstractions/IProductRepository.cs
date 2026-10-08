using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Abstractions;

/// <param name="Active">null = todos, true = solo activos, false = solo inactivos.</param>
public sealed record ProductFilter(string? Term, string? Category, bool? Active);

public interface IProductRepository
{
    /// <summary>Term busca por nombre, codigo o categoria.</summary>
    Task<PagedResult<Product>> ListAsync(ProductFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Todos los que cumplen el filtro, sin paginar (exportaciones).</summary>
    Task<IReadOnlyList<Product>> ListAllAsync(ProductFilter filter, CancellationToken cancellationToken = default);

    Task<Product?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Rastreados, para actualizarlos en la carga masiva. Los SKU ya vienen normalizados.</summary>
    Task<IReadOnlyList<Product>> FindBySkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default);

    /// <summary>Bloquea las filas con FOR UPDATE; usar dentro de una transaccion.</summary>
    Task<IReadOnlyList<Product>> FindByIdsForUpdateAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>La categoria ya registrada que coincide sin mayusculas ni tildes, o null.</summary>
    Task<string?> FindCategoryAsync(string category, CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(string sku, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> HasSalesAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    void Remove(Product product);
}
