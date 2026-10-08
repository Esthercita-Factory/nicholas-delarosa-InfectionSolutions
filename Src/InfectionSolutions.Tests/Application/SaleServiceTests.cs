using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Sales;
using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Tests.Application;

public class SaleServiceTests
{
    private readonly ISaleRepository _sales = Substitute.For<ISaleRepository>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly SaleService _service;

    private readonly Customer _cliente = new() { Id = Guid.NewGuid(), FullName = "Laura Mendoza", Document = "1020", Email = "laura@correo.com", Phone = "3000000000", IsActive = true };
    private readonly Product _cemento = new() { Id = Guid.NewGuid(), Sku = "CEM-50", Name = "Cemento", Unit = "Bulto", Price = 30_000m, Stock = 10, IsActive = true };
    private readonly Product _varilla = new() { Id = Guid.NewGuid(), Sku = "VAR-12", Name = "Varilla", Unit = "Unidad", Price = 15_000m, Stock = 2, IsActive = true };

    public SaleServiceTests()
    {
        _service = new SaleService(_sales, _products, _customers, _unitOfWork, _clock);

        _customers.FindByIdAsync(_cliente.Id, Arg.Any<CancellationToken>()).Returns(_cliente);
        _products.FindByIdsForUpdateAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Product> { _cemento, _varilla });
        _sales.CountByNumberPrefixAsync("V-20261007-", Arg.Any<CancellationToken>()).Returns(4);
    }

    [Fact]
    public async Task CreateAsync_CalculaTotalesYDescuentaStock()
    {
        var solicitud = new SaleRequest
        {
            CustomerId = _cliente.Id,
            Lines = [new SaleLineRequest(_cemento.Id, 3), new SaleLineRequest(_varilla.Id, 2)]
        };

        var resultado = await _service.CreateAsync(solicitud, "admin-id");

        Assert.True(resultado.IsSuccess);
        var venta = resultado.Value!;
        Assert.Equal("V-20261007-0005", venta.SaleNumber);
        Assert.Equal(120_000m, venta.Subtotal);
        Assert.Equal(22_800m, venta.Tax);
        Assert.Equal(142_800m, venta.Total);
        Assert.Equal(SaleStatus.Completed, venta.Status);
        Assert.Equal(7, _cemento.Stock);
        Assert.Equal(0, _varilla.Stock);
        Assert.True(_unitOfWork.Committed);
    }

    [Fact]
    public async Task CreateAsync_SumaLineasRepetidasDelMismoProducto()
    {
        var solicitud = new SaleRequest
        {
            CustomerId = _cliente.Id,
            Lines = [new SaleLineRequest(_cemento.Id, 2), new SaleLineRequest(_cemento.Id, 3)]
        };

        var resultado = await _service.CreateAsync(solicitud, null);

        Assert.True(resultado.IsSuccess);
        Assert.Single(resultado.Value!.Details);
        Assert.Equal(5, resultado.Value.Details[0].Quantity);
        Assert.Equal(5, _cemento.Stock);
    }

    [Fact]
    public async Task CreateAsync_SinStockSuficiente_NoModificaNada()
    {
        var solicitud = new SaleRequest
        {
            CustomerId = _cliente.Id,
            Lines = [new SaleLineRequest(_cemento.Id, 1), new SaleLineRequest(_varilla.Id, 5)]
        };

        var resultado = await _service.CreateAsync(solicitud, null);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorCode.BusinessRule, resultado.Error!.Code);
        Assert.Contains("Stock insuficiente", resultado.Error.Message);
        // La validacion ocurre antes de tocar cualquier producto.
        Assert.Equal(10, _cemento.Stock);
        Assert.Equal(2, _varilla.Stock);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ConClienteInactivo_Falla()
    {
        _cliente.IsActive = false;

        var resultado = await _service.CreateAsync(new SaleRequest
        {
            CustomerId = _cliente.Id,
            Lines = [new SaleLineRequest(_cemento.Id, 1)]
        }, null);

        Assert.True(resultado.IsFailure);
        Assert.Equal(nameof(SaleRequest.CustomerId), resultado.Error!.Field);
    }

    [Fact]
    public async Task CancelAsync_DevuelveElStock()
    {
        var venta = new Sale
        {
            Id = Guid.NewGuid(),
            Status = SaleStatus.Completed,
            Details = [new SaleDetail { Product = _cemento, ProductId = _cemento.Id, Quantity = 4 }]
        };
        _sales.FindByIdAsync(venta.Id, Arg.Any<CancellationToken>()).Returns(venta);

        var resultado = await _service.CancelAsync(venta.Id);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(SaleStatus.Cancelled, venta.Status);
        Assert.Equal(14, _cemento.Stock);
        Assert.Equal(_clock.UtcNow, venta.CancelledAt);
    }
}
