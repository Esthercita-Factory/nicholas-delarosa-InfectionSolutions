using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Portal;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Notifications;
using InfectionSolutions.Application.Services.Portal;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Application.Services.Sales;
using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Tests.Application;

public class CustomerPortalServiceTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IProductService _products = Substitute.For<IProductService>();
    private readonly ISaleService _sales = Substitute.For<ISaleService>();
    private readonly IReceiptService _receipts = Substitute.For<IReceiptService>();
    private readonly ICustomerNotifier _notifier = Substitute.For<ICustomerNotifier>();
    private readonly CustomerPortalService _service;

    private readonly Customer _laura = new() { Id = Guid.NewGuid(), UserId = "user-laura", FullName = "Laura", Email = "laura@correo.com", IsActive = true };

    public CustomerPortalServiceTests()
    {
        _service = new CustomerPortalService(_customers, _products, _sales, _receipts, _notifier);
        _customers.FindByUserIdAsync("user-laura", Arg.Any<CancellationToken>()).Returns(_laura);
    }

    private static SaleResponse Venta(Guid clienteId) => new(
        Guid.NewGuid(), "V-20261008-0001", DateTimeOffset.UtcNow, SaleStatus.Completed, clienteId,
        "Laura", "1020", "laura@correo.com", "300", null, 100m, 19m, 119m, null, null, []);

    [Fact]
    public async Task Compra_UsaElClienteDelToken_YEnviaElComprobante()
    {
        var venta = Venta(_laura.Id);
        _sales.CreateAsync(Arg.Any<SaleRequest>(), "user-laura", Arg.Any<CancellationToken>()).Returns(Result.Success(venta));
        _notifier.SendPurchaseConfirmationAsync(venta, Arg.Any<CancellationToken>()).Returns(true);

        var resultado = await _service.CheckoutAsync("user-laura", new CheckoutRequest
        {
            Lines = [new SaleLineRequest(Guid.NewGuid(), 2)]
        });

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value!.ReceiptEmailSent);
        Assert.Equal("laura@correo.com", resultado.Value.SentTo);
        await _sales.Received(1).CreateAsync(Arg.Is<SaleRequest>(r => r.CustomerId == _laura.Id), "user-laura", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Compra_SiElCorreoFalla_LaCompraIgualQuedaRegistrada()
    {
        var venta = Venta(_laura.Id);
        _sales.CreateAsync(Arg.Any<SaleRequest>(), "user-laura", Arg.Any<CancellationToken>()).Returns(Result.Success(venta));
        _notifier.SendPurchaseConfirmationAsync(venta, Arg.Any<CancellationToken>()).Returns(false);

        var resultado = await _service.CheckoutAsync("user-laura", new CheckoutRequest { Lines = [new SaleLineRequest(Guid.NewGuid(), 1)] });

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value!.ReceiptEmailSent);
    }

    [Fact]
    public async Task Compra_CuentaSinFichaDeCliente_EsProhibida()
    {
        var resultado = await _service.CheckoutAsync("user-sin-ficha", new CheckoutRequest { Lines = [new SaleLineRequest(Guid.NewGuid(), 1)] });

        Assert.Equal(ErrorCode.Forbidden, resultado.Error!.Code);
        await _sales.DidNotReceiveWithAnyArgs().CreateAsync(default!, default, default);
    }

    [Fact]
    public async Task VentaDeOtroCliente_SeRespondeComoInexistente()
    {
        var ajena = Venta(Guid.NewGuid());
        _sales.GetByIdAsync(ajena.Id, Arg.Any<CancellationToken>()).Returns(Result.Success(ajena));

        var detalle = await _service.GetMySaleAsync("user-laura", ajena.Id);
        var recibo = await _service.GetMyReceiptAsync("user-laura", ajena.Id);

        Assert.Equal(ErrorCode.NotFound, detalle.Error!.Code);
        Assert.Equal(ErrorCode.NotFound, recibo.Error!.Code);
        await _receipts.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Fact]
    public async Task MisCompras_FiltraPorElClienteDelToken()
    {
        _sales.ListAsync(Arg.Any<SaleQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResponse<SaleListItemResponse> { Items = [] });

        await _service.ListMySalesAsync("user-laura", new SaleQuery { CustomerId = Guid.NewGuid() });

        await _sales.Received(1).ListAsync(Arg.Is<SaleQuery>(q => q.CustomerId == _laura.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Catalogo_SoloMuestraProductosActivos()
    {
        _products.ListAsync(Arg.Any<ProductQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResponse<ProductResponse> { Items = [] });

        await _service.ListCatalogAsync(new ProductQuery { Active = false });

        await _products.Received(1).ListAsync(Arg.Is<ProductQuery>(q => q.Active == true), Arg.Any<CancellationToken>());
    }
}
