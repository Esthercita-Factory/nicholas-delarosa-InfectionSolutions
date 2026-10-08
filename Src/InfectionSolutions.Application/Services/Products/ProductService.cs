using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Services.Products;

/// <summary>
/// Unico punto donde la entidad Product se convierte en DTO y al reves:
/// fuera de Application/Infrastructure la entidad no circula.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ProductService(IProductRepository products, IUnitOfWork unitOfWork, IClock clock)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<PagedResponse<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };
        var filtro = new ProductFilter(LimpiarOpcional(query.Q), LimpiarOpcional(query.Category), query.OnlyActive);
        var result = await _products.ListAsync(filtro, page, cancellationToken);

        return PagedResponse<ProductResponse>.From(result, page, ToResponse);
    }

    public async Task<Result<ProductResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _products.FindByIdAsync(id, cancellationToken);

        return product is null
            ? Result.Failure<ProductResponse>(Error.EntityNotFound("Producto", id))
            : Result.Success(ToResponse(product));
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => _products.GetCategoriesAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _products.GetActiveAsync(cancellationToken);
        return products
            .Select(p => new ProductOption(p.Id, p.Sku, p.Name, p.Unit, p.Price, p.Stock))
            .ToList();
    }

    public async Task<Result<ProductResponse>> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var error = Validar(request);
        if (error is not null)
        {
            return Result.Failure<ProductResponse>(error);
        }

        var sku = NormalizarSku(request.Sku);
        if (await _products.SkuExistsAsync(sku, null, cancellationToken))
        {
            return Result.Failure<ProductResponse>(SkuDuplicado());
        }

        var product = new Product
        {
            Sku = sku,
            CreatedAt = _clock.UtcNow
        };

        AplicarCambios(request, product);
        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(product));
    }

    public async Task<Result<ProductResponse>> UpdateAsync(Guid id, ProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _products.FindByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductResponse>(Error.EntityNotFound("Producto", id));
        }

        var error = Validar(request);
        if (error is not null)
        {
            return Result.Failure<ProductResponse>(error);
        }

        var sku = NormalizarSku(request.Sku);
        if (await _products.SkuExistsAsync(sku, id, cancellationToken))
        {
            return Result.Failure<ProductResponse>(SkuDuplicado());
        }

        AplicarCambios(request, product);
        product.Sku = sku;
        product.UpdatedAt = _clock.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(product));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _products.FindByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.EntityNotFound("Producto", id));
        }

        if (await _products.HasSalesAsync(id, cancellationToken))
        {
            return Result.Failure(Error.Conflict(
                "No se puede eliminar un producto con ventas registradas. Desactívalo para ocultarlo del catálogo."));
        }

        _products.Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Reglas minimas que deben cumplirse sin importar quien llame (panel o
    /// API); los formularios validan lo mismo antes con Data Annotations.
    /// </summary>
    private static Error? Validar(ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            return Error.Validation("El código es obligatorio.", nameof(request.Sku));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("El nombre es obligatorio.", nameof(request.Name));
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return Error.Validation("La categoría es obligatoria.", nameof(request.Category));
        }

        if (request.Price <= 0)
        {
            return Error.Validation("El precio debe ser mayor que cero.", nameof(request.Price));
        }

        if (request.Stock < 0)
        {
            return Error.Validation("El stock no puede ser negativo.", nameof(request.Stock));
        }

        return null;
    }

    private static void AplicarCambios(ProductRequest request, Product product)
    {
        product.Name = request.Name.Trim();
        product.Description = LimpiarOpcional(request.Description);
        product.Category = request.Category.Trim();
        product.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Unidad" : request.Unit.Trim();
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.IsActive = request.IsActive;
    }

    private static ProductResponse ToResponse(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Category,
        product.Unit,
        product.Price,
        product.Stock,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);

    /// <summary>sku es unique: se guarda en mayusculas para no duplicar por mayusculas/minusculas.</summary>
    private static string NormalizarSku(string sku) => sku.Trim().ToUpperInvariant();

    private static Error SkuDuplicado() => Error.Conflict("Ya existe un producto con ese código.", nameof(ProductRequest.Sku));

    private static string? LimpiarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
