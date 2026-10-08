using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Domain.Enums;
using InfectionSolutions.Domain.Services;

namespace InfectionSolutions.Application.Services.Sales;

public sealed class SaleService : ISaleService
{
    public const int MaxLines = 50;

    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SaleService(
        ISaleRepository sales,
        IProductRepository products,
        ICustomerRepository customers,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _sales = sales;
        _products = products;
        _customers = customers;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<PagedResponse<SaleListItemResponse>> ListAsync(SaleQuery query, CancellationToken cancellationToken = default)
    {
        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };
        var termino = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var result = await _sales.ListAsync(new SaleFilter(termino, query.Status), page, cancellationToken);

        return PagedResponse<SaleListItemResponse>.From(result, page, ToListItem);
    }

    public async Task<Result<SaleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await _sales.FindByIdAsync(id, cancellationToken);

        return sale is null
            ? Result.Failure<SaleResponse>(Error.EntityNotFound("Venta", id))
            : Result.Success(ToResponse(sale));
    }

    public async Task<Result<SaleResponse>> CreateAsync(SaleRequest request, string? userId, CancellationToken cancellationToken = default)
    {
        // Si el mismo producto viene en dos lineas se suman: el stock se
        // valida contra la cantidad total.
        var lineas = request.Lines
            .GroupBy(line => line.ProductId)
            .Select(group => new SaleLineRequest(group.Key, group.Sum(line => line.Quantity)))
            .ToList();

        if (lineas.Count == 0)
        {
            return Result.Failure<SaleResponse>(Error.Validation("Agrega al menos un producto a la venta.", nameof(request.Lines)));
        }

        if (lineas.Count > MaxLines)
        {
            return Result.Failure<SaleResponse>(Error.Validation($"Una venta admite máximo {MaxLines} productos distintos.", nameof(request.Lines)));
        }

        if (lineas.Any(line => line.Quantity <= 0))
        {
            return Result.Failure<SaleResponse>(Error.Validation("Todas las cantidades deben ser mayores que cero.", nameof(request.Lines)));
        }

        var customer = await _customers.FindByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null || !customer.IsActive)
        {
            return Result.Failure<SaleResponse>(Error.Validation("El cliente no existe o está inactivo.", nameof(request.CustomerId)));
        }

        await using var transaccion = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // FOR UPDATE: dos ventas simultaneas no pueden descontar el mismo stock.
        var productos = (await _products.FindByIdsForUpdateAsync(lineas.Select(line => line.ProductId).ToList(), cancellationToken))
            .ToDictionary(product => product.Id);

        var ahora = _clock.UtcNow;
        var sale = new Sale
        {
            CustomerId = customer.Id,
            Customer = customer,
            SaleDate = ahora,
            Status = SaleStatus.Completed,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedByUserId = userId
        };

        // Primero se valida todo y luego se modifica: asi un error en la ultima
        // linea no deja productos con stock descontado en el contexto.
        foreach (var linea in lineas)
        {
            if (!productos.TryGetValue(linea.ProductId, out var product) || !product.IsActive)
            {
                return Result.Failure<SaleResponse>(Error.BusinessRule("Uno de los productos no existe o está inactivo.", nameof(request.Lines)));
            }

            if (linea.Quantity > product.Stock)
            {
                return Result.Failure<SaleResponse>(Error.BusinessRule(
                    $"Stock insuficiente para {product.Name}: disponible {product.Stock}, solicitado {linea.Quantity}.",
                    nameof(request.Lines)));
            }
        }

        foreach (var linea in lineas)
        {
            var product = productos[linea.ProductId];
            product.Stock = InventoryCalculator.DiscountStock(product.Stock, linea.Quantity);
            product.UpdatedAt = ahora;

            sale.Details.Add(new SaleDetail
            {
                ProductId = product.Id,
                Product = product,
                Quantity = linea.Quantity,
                UnitPrice = product.Price,
                LineTotal = InventoryCalculator.CalculateLineTotal(linea.Quantity, product.Price)
            });
        }

        var totales = InventoryCalculator.CalculateTotals(sale.Details.Select(d => (d.Quantity, d.UnitPrice)));
        sale.Subtotal = totales.Subtotal;
        sale.Tax = totales.Tax;
        sale.Total = totales.Total;

        var prefijo = SaleNumberGenerator.DayPrefix(ahora);
        var consecutivo = await _sales.CountByNumberPrefixAsync(prefijo, cancellationToken) + 1;
        sale.SaleNumber = SaleNumberGenerator.Generate(ahora, consecutivo);

        await _sales.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return Result.Success(ToResponse(sale));
    }

    public async Task<Result> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await _sales.FindByIdAsync(id, cancellationToken);
        if (sale is null)
        {
            return Result.Failure(Error.EntityNotFound("Venta", id));
        }

        if (sale.Status == SaleStatus.Cancelled)
        {
            return Result.Failure(Error.BusinessRule("La venta ya estaba anulada."));
        }

        var ahora = _clock.UtcNow;
        foreach (var detail in sale.Details)
        {
            detail.Product.Stock += detail.Quantity;
            detail.Product.UpdatedAt = ahora;
        }

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledAt = ahora;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static SaleListItemResponse ToListItem(Sale sale) => new(
        sale.Id,
        sale.SaleNumber,
        sale.SaleDate,
        sale.Customer.FullName,
        sale.Customer.Document,
        sale.Details.Count,
        sale.Total,
        sale.Status);

    private static SaleResponse ToResponse(Sale sale) => new(
        sale.Id,
        sale.SaleNumber,
        sale.SaleDate,
        sale.Status,
        sale.CustomerId,
        sale.Customer.FullName,
        sale.Customer.Document,
        sale.Customer.Email,
        sale.Customer.Phone,
        sale.Subtotal,
        sale.Tax,
        sale.Total,
        sale.Notes,
        sale.CancelledAt,
        sale.Details
            .Select(d => new SaleDetailResponse(d.ProductId, d.Product.Sku, d.Product.Name, d.Product.Unit, d.Quantity, d.UnitPrice, d.LineTotal))
            .ToList());
}
