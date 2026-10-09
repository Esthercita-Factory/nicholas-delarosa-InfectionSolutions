using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Portal;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Receipts;

namespace InfectionSolutions.Application.Services.Portal;

/// <summary>
/// Casos de uso del rol Cliente. Todo se resuelve a partir del usuario
/// autenticado: un cliente solo ve el catalogo activo y sus propias compras.
/// </summary>
public interface ICustomerPortalService
{
    /// <summary>Solo productos activos, sin importar lo que pida el filtro.</summary>
    Task<PagedResponse<ProductResponse>> ListCatalogAsync(ProductQuery query, CancellationToken cancellationToken = default);

    Task<Result<ProductResponse>> GetCatalogProductAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<SaleListItemResponse>>> ListMySalesAsync(string userId, SaleQuery query, CancellationToken cancellationToken = default);

    Task<Result<SaleResponse>> GetMySaleAsync(string userId, Guid saleId, CancellationToken cancellationToken = default);

    /// <summary>Registra la compra (mismas reglas de stock e IVA del panel) y envia el comprobante por correo.</summary>
    Task<Result<SaleCreatedResponse>> CheckoutAsync(string userId, CheckoutRequest request, CancellationToken cancellationToken = default);

    Task<Result<ReceiptFile>> GetMyReceiptAsync(string userId, Guid saleId, CancellationToken cancellationToken = default);
}
