using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Portal;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Notifications;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Application.Services.Sales;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Services.Portal;

public sealed class CustomerPortalService : ICustomerPortalService
{
    private readonly ICustomerRepository _customers;
    private readonly IProductService _products;
    private readonly ISaleService _sales;
    private readonly IReceiptService _receipts;
    private readonly ICustomerNotifier _notifier;

    public CustomerPortalService(
        ICustomerRepository customers,
        IProductService products,
        ISaleService sales,
        IReceiptService receipts,
        ICustomerNotifier notifier)
    {
        _customers = customers;
        _products = products;
        _sales = sales;
        _receipts = receipts;
        _notifier = notifier;
    }

    public Task<PagedResponse<ProductResponse>> ListCatalogAsync(ProductQuery query, CancellationToken cancellationToken = default)
        => _products.ListAsync(query with { Active = true }, cancellationToken);

    public async Task<Result<ProductResponse>> GetCatalogProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var producto = await _products.GetByIdAsync(productId, cancellationToken);

        // Un producto inactivo no existe para el cliente.
        return producto.IsSuccess && !producto.Value!.IsActive
            ? Result.Failure<ProductResponse>(Error.EntityNotFound("Producto", productId))
            : producto;
    }

    public async Task<Result<PagedResponse<SaleListItemResponse>>> ListMySalesAsync(string userId, SaleQuery query, CancellationToken cancellationToken = default)
    {
        var cliente = await ClienteDelUsuarioAsync(userId, exigirActivo: false, cancellationToken);
        if (cliente.IsFailure)
        {
            return Result.Failure<PagedResponse<SaleListItemResponse>>(cliente.Error!);
        }

        return Result.Success(await _sales.ListAsync(query with { CustomerId = cliente.Value!.Id }, cancellationToken));
    }

    public async Task<Result<SaleResponse>> GetMySaleAsync(string userId, Guid saleId, CancellationToken cancellationToken = default)
    {
        var cliente = await ClienteDelUsuarioAsync(userId, exigirActivo: false, cancellationToken);
        if (cliente.IsFailure)
        {
            return Result.Failure<SaleResponse>(cliente.Error!);
        }

        var venta = await _sales.GetByIdAsync(saleId, cancellationToken);

        // La venta de otro cliente responde igual que una inexistente: no se revela que existe.
        return venta.IsSuccess && venta.Value!.CustomerId != cliente.Value!.Id
            ? Result.Failure<SaleResponse>(Error.EntityNotFound("Venta", saleId))
            : venta;
    }

    public async Task<Result<SaleCreatedResponse>> CheckoutAsync(string userId, CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var cliente = await ClienteDelUsuarioAsync(userId, exigirActivo: true, cancellationToken);
        if (cliente.IsFailure)
        {
            return Result.Failure<SaleCreatedResponse>(cliente.Error!);
        }

        var venta = await _sales.CreateAsync(new SaleRequest
        {
            CustomerId = cliente.Value!.Id,
            Lines = request.Lines,
            Notes = request.Notes
        }, userId, cancellationToken);

        if (venta.IsFailure)
        {
            return Result.Failure<SaleCreatedResponse>(venta.Error!);
        }

        var enviado = await _notifier.SendPurchaseConfirmationAsync(venta.Value!, cancellationToken);

        return Result.Success(new SaleCreatedResponse(venta.Value!, enviado, venta.Value!.CustomerEmail));
    }

    public async Task<Result<ReceiptFile>> GetMyReceiptAsync(string userId, Guid saleId, CancellationToken cancellationToken = default)
    {
        var venta = await GetMySaleAsync(userId, saleId, cancellationToken);
        if (venta.IsFailure)
        {
            return Result.Failure<ReceiptFile>(venta.Error!);
        }

        return await _receipts.GetAsync(saleId, cancellationToken);
    }

    private async Task<Result<Customer>> ClienteDelUsuarioAsync(string userId, bool exigirActivo, CancellationToken cancellationToken)
    {
        var cliente = await _customers.FindByUserIdAsync(userId, cancellationToken);
        if (cliente is null)
        {
            return Result.Failure<Customer>(Error.Forbidden("Tu cuenta no tiene una ficha de cliente asociada."));
        }

        if (exigirActivo && !cliente.IsActive)
        {
            return Result.Failure<Customer>(Error.Forbidden("Tu cuenta de cliente está inactiva. Comunícate con la empresa."));
        }

        return Result.Success(cliente);
    }
}
