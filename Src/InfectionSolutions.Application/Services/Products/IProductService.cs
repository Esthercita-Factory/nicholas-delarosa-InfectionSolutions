using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Products;

namespace InfectionSolutions.Application.Services.Products;

public interface IProductService
{
    Task<PagedResponse<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken = default);

    Task<Result<ProductResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Productos activos para el formulario de ventas.</summary>
    Task<IReadOnlyList<ProductOption>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<Result<ProductResponse>> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProductResponse>> UpdateAsync(Guid id, ProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>Falla si el producto tiene ventas: en ese caso se desactiva en lugar de borrarlo.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
