using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Services.Products;

namespace InfectionSolutions.Tests.Application;

public class ProductServiceTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(_products, _unitOfWork, _clock);
    }

    private static ProductRequest Solicitud(string sku = " cem-50 ", decimal precio = 32_000m) => new()
    {
        Sku = sku,
        Name = "  Cemento gris 50 kg ",
        Category = "Cementos",
        Unit = "Bulto",
        Price = precio,
        Stock = 100,
        IsActive = true
    };

    [Fact]
    public async Task CreateAsync_NormalizaYGuarda()
    {
        var resultado = await _service.CreateAsync(Solicitud());

        Assert.True(resultado.IsSuccess);
        Assert.Equal("CEM-50", resultado.Value!.Sku);
        Assert.Equal("Cemento gris 50 kg", resultado.Value.Name);
        Assert.Equal(_clock.UtcNow, resultado.Value.CreatedAt);
        await _products.Received(1).AddAsync(Arg.Is<Product>(p => p.Sku == "CEM-50"), Arg.Any<CancellationToken>());
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ConSkuDuplicado_DevuelveConflictoSinGuardar()
    {
        _products.SkuExistsAsync("CEM-50", null, Arg.Any<CancellationToken>()).Returns(true);

        var resultado = await _service.CreateAsync(Solicitud());

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorCode.Conflict, resultado.Error!.Code);
        Assert.Equal(nameof(ProductRequest.Sku), resultado.Error.Field);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ConPrecioCero_FallaLaValidacion()
    {
        var resultado = await _service.CreateAsync(Solicitud(precio: 0));

        Assert.True(resultado.IsFailure);
        Assert.Equal(nameof(ProductRequest.Price), resultado.Error!.Field);
        await _products.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ConVentas_NoElimina()
    {
        var producto = new Product { Id = Guid.NewGuid(), Sku = "CEM-50", Name = "Cemento" };
        _products.FindByIdAsync(producto.Id, Arg.Any<CancellationToken>()).Returns(producto);
        _products.HasSalesAsync(producto.Id, Arg.Any<CancellationToken>()).Returns(true);

        var resultado = await _service.DeleteAsync(producto.Id);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorCode.Conflict, resultado.Error!.Code);
        _products.DidNotReceive().Remove(Arg.Any<Product>());
    }
}
