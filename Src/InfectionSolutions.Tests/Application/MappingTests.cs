using InfectionSolutions.Api.ViewModels.Products;
using InfectionSolutions.Api.ViewModels.Sales;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Tests.Application;

public class MappingTests
{
    [Fact]
    public void LaConfiguracionDeAutoMapperEsValida()
    {
        // Falla si algun miembro del destino quedo sin origen.
        TestMapper.Configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void Cliente_ConUsuario_TieneAccesoAlPortal()
    {
        var conCuenta = new Customer { Id = Guid.NewGuid(), FullName = "Laura", UserId = "user-1" };
        var sinCuenta = new Customer { Id = Guid.NewGuid(), FullName = "Pedro" };

        Assert.True(TestMapper.Instance.Map<CustomerResponse>(conCuenta).HasPortalAccess);
        Assert.False(TestMapper.Instance.Map<CustomerResponse>(sinCuenta).HasPortalAccess);
    }

    [Fact]
    public void Venta_SeAplanaConClienteYProductos()
    {
        var producto = new Product { Id = Guid.NewGuid(), Sku = "CEM-50", Name = "Cemento", Unit = "Bulto" };
        var venta = new Sale
        {
            Id = Guid.NewGuid(),
            SaleNumber = "V-20261008-0001",
            Status = SaleStatus.Completed,
            Customer = new Customer { FullName = "Laura Mendoza", Document = "1020", Email = "l@c.com", Phone = "300", Address = "Calle 1" },
            Subtotal = 64_000m,
            Tax = 12_160m,
            Total = 76_160m,
            Details = [new SaleDetail { ProductId = producto.Id, Product = producto, Quantity = 2, UnitPrice = 32_000m, LineTotal = 64_000m }]
        };

        var detalle = TestMapper.Instance.Map<SaleResponse>(venta);
        var fila = TestMapper.Instance.Map<SaleListItemResponse>(venta);

        Assert.Equal("Laura Mendoza", detalle.CustomerName);
        Assert.Equal("Calle 1", detalle.CustomerAddress);
        var linea = Assert.Single(detalle.Details);
        Assert.Equal(("CEM-50", "Cemento", "Bulto"), (linea.Sku, linea.ProductName, linea.Unit));
        Assert.Equal(1, fila.ItemCount);
        Assert.Equal("1020", fila.CustomerDocument);
    }

    [Fact]
    public void ViewModelDeLaApi_SeConvierteEnDtoDeApplication()
    {
        var producto = TestMapper.Instance.Map<ProductRequest>(new ProductViewModel
        {
            Sku = "cem-50", Name = "Cemento", Category = "Cementos", Unit = "Bulto", Price = 32_000m, Stock = 10
        });
        var venta = TestMapper.Instance.Map<SaleRequest>(new SaleViewModel
        {
            CustomerId = Guid.NewGuid(),
            Lines = [new SaleLineViewModel { ProductId = Guid.NewGuid(), Quantity = 3 }]
        });

        Assert.Equal((32_000m, 10), (producto.Price, producto.Stock));
        Assert.Equal(3, Assert.Single(venta.Lines).Quantity);
    }
}
