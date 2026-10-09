using AutoMapper;
using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Domain.Enums;
using InfectionSolutions.Domain.Services;
using Microsoft.Extensions.Logging;

namespace InfectionSolutions.Application.Services.Sales;

public sealed class SaleService : ISaleService
{
    public const int MaxLines = 50;

    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IReceiptService _receipts;
    private readonly ILogger<SaleService> _logger;
    private readonly IMapper _mapper;

    public SaleService(
        ISaleRepository sales,
        IProductRepository products,
        ICustomerRepository customers,
        IUnitOfWork unitOfWork,
        IClock clock,
        IReceiptService receipts,
        ILogger<SaleService> logger,
        IMapper mapper)
    {
        _sales = sales;
        _products = products;
        _customers = customers;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _receipts = receipts;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<PagedResponse<SaleListItemResponse>> ListAsync(SaleQuery query, CancellationToken cancellationToken = default)
    {
        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };
        var termino = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var result = await _sales.ListAsync(new SaleFilter(termino, query.Status, query.CustomerId), page, cancellationToken);

        return PagedResponse<SaleListItemResponse>.From(result, page, _mapper.Map<SaleListItemResponse>);
    }

    public async Task<IReadOnlyList<SaleListItemResponse>> ListAllAsync(SaleQuery query, CancellationToken cancellationToken = default)
    {
        var termino = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var sales = await _sales.ListAllAsync(new SaleFilter(termino, query.Status, query.CustomerId), cancellationToken);
        return _mapper.Map<List<SaleListItemResponse>>(sales);
    }

    public async Task<Result<SaleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await _sales.FindByIdAsync(id, cancellationToken);

        return sale is null
            ? Result.Failure<SaleResponse>(Error.EntityNotFound("Venta", id))
            : Result.Success(_mapper.Map<SaleResponse>(sale));
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

        // El consecutivo es por dia de Colombia: a las 8 p. m. ya es mañana en UTC.
        var diaDeNegocio = DisplayFormats.ToBusinessTime(ahora);
        var prefijo = SaleNumberGenerator.DayPrefix(diaDeNegocio);
        var consecutivo = await _sales.CountByNumberPrefixAsync(prefijo, cancellationToken) + 1;
        sale.SaleNumber = SaleNumberGenerator.Generate(diaDeNegocio, consecutivo);

        await _sales.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        var respuesta = _mapper.Map<SaleResponse>(sale);
        await GuardarReciboAsync(respuesta, cancellationToken);

        return Result.Success(respuesta);
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

        // El recibo se regenera para que diga ANULADA.
        await GuardarReciboAsync(_mapper.Map<SaleResponse>(sale), cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// La venta ya quedo guardada: si el PDF falla no se deshace, se registra
    /// y el recibo se vuelve a generar cuando alguien lo descargue.
    /// </summary>
    private async Task GuardarReciboAsync(SaleResponse sale, CancellationToken cancellationToken)
    {
        try
        {
            await _receipts.SaveAsync(sale, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se pudo generar el recibo de la venta {SaleNumber}", sale.SaleNumber);
        }
    }
}
