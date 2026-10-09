using System.Security.Claims;
using InfectionSolutions.Api.Controllers;
using InfectionSolutions.Api.ViewModels.Products;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Services.Portal;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Tests.Api;

public class ProductsControllerTests
{
    private readonly IProductService _products = Substitute.For<IProductService>();
    private readonly ICustomerPortalService _portal = Substitute.For<ICustomerPortalService>();

    private ProductsController Controlador(string rol)
    {
        var identidad = new ClaimsIdentity([new Claim("sub", "user-1"), new Claim("role", rol)], "Bearer", "name", "role");
        return new ProductsController(_products, _portal, TestMapper.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
            }
        };
    }

    private static ProductResponse Producto(bool activo = true) => new(
        Guid.NewGuid(), "CEM-50", "Cemento", null, "Cementos", "Bulto", 32_000m, 10, activo, DateTimeOffset.UtcNow, null);

    [Fact]
    public async Task Listado_ParaUnCliente_UsaElCatalogoDeActivos()
    {
        _portal.ListCatalogAsync(Arg.Any<ProductQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResponse<ProductResponse> { Items = [Producto()] });

        var respuesta = await Controlador(ApplicationRoles.Customer).List(new ProductQuery { Active = false }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(respuesta.Result);
        await _products.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
    }

    [Fact]
    public async Task Detalle_Inexistente_Devuelve404ConProblemDetails()
    {
        var id = Guid.NewGuid();
        _products.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ProductResponse>(Error.EntityNotFound("Producto", id)));

        var respuesta = await Controlador(ApplicationRoles.Administrator).GetById(id, CancellationToken.None);

        var resultado = Assert.IsType<ObjectResult>(respuesta.Result);
        Assert.Equal(StatusCodes.Status404NotFound, resultado.StatusCode);
        Assert.Contains(id.ToString(), Assert.IsType<ProblemDetails>(resultado.Value).Detail);
    }

    [Fact]
    public async Task Crear_ConCodigoRepetido_Devuelve409ConElCampo()
    {
        _products.CreateAsync(Arg.Any<ProductRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ProductResponse>(Error.Conflict("Ya existe un producto con ese código.", nameof(ProductRequest.Sku))));

        var respuesta = await Controlador(ApplicationRoles.Administrator).Create(new ProductViewModel
        {
            Sku = "CEM-50", Name = "Cemento", Category = "Cementos", Unit = "Bulto", Price = 32_000m, Stock = 5
        }, CancellationToken.None);

        var resultado = Assert.IsType<ObjectResult>(respuesta.Result);
        Assert.Equal(StatusCodes.Status409Conflict, resultado.StatusCode);
        var problema = Assert.IsType<ValidationProblemDetails>(resultado.Value);
        Assert.Equal(["Ya existe un producto con ese código."], problema.Errors["sku"]);
    }

    [Fact]
    public async Task Crear_Correcto_Devuelve201ConLaUbicacion()
    {
        var creado = Producto();
        _products.CreateAsync(Arg.Is<ProductRequest>(r => r.Sku == "cem-50" && r.Price == 32_000m), Arg.Any<CancellationToken>())
            .Returns(Result.Success(creado));

        var respuesta = await Controlador(ApplicationRoles.Administrator).Create(new ProductViewModel
        {
            Sku = "cem-50", Name = "Cemento", Category = "Cementos", Unit = "Bulto", Price = 32_000m, Stock = 5
        }, CancellationToken.None);

        var resultado = Assert.IsType<CreatedAtActionResult>(respuesta.Result);
        Assert.Equal(nameof(ProductsController.GetById), resultado.ActionName);
        Assert.Same(creado, resultado.Value);
    }

    [Fact]
    public async Task Eliminar_ConVentas_Devuelve409()
    {
        var id = Guid.NewGuid();
        _products.DeleteAsync(id, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Conflict("No se puede eliminar un producto con ventas registradas.")));

        var respuesta = await Controlador(ApplicationRoles.Administrator).Delete(id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(respuesta).StatusCode);
    }
}
