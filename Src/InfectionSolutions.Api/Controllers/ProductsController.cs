using AutoMapper;
using InfectionSolutions.Api.Common;
using InfectionSolutions.Api.ViewModels.Products;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Services.Portal;
using InfectionSolutions.Application.Services.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Api.Controllers;

/// <summary>
/// Catálogo de productos. Lectura para cualquier usuario autenticado (el cliente
/// solo ve los activos); crear, editar y eliminar es solo para administradores.
/// </summary>
[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    private readonly ICustomerPortalService _portal;
    private readonly IMapper _mapper;

    public ProductsController(IProductService products, ICustomerPortalService portal, IMapper mapper)
    {
        _products = products;
        _portal = portal;
        _mapper = mapper;
    }

    /// <summary>Lista paginada con búsqueda (nombre, código o categoría; sin importar mayúsculas ni tildes).</summary>
    /// <remarks>Para un cliente el filtro "active" se ignora: siempre recibe solo productos activos.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> List([FromQuery] ProductQuery query, CancellationToken cancellationToken)
        => Ok(User.IsAdministrator()
            ? await _products.ListAsync(query, cancellationToken)
            : await _portal.ListCatalogAsync(query, cancellationToken));

    /// <summary>Categorías registradas, para los filtros.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Categories(CancellationToken cancellationToken)
        => Ok(await _products.GetCategoriesAsync(cancellationToken));

    /// <summary>Detalle de un producto.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => this.OkOrProblem(User.IsAdministrator()
            ? await _products.GetByIdAsync(id, cancellationToken)
            : await _portal.GetCatalogProductAsync(id, cancellationToken));

    /// <summary>Crea un producto. Solo administradores.</summary>
    /// <response code="201">Producto creado.</response>
    /// <response code="409">Ya existe un producto con ese código.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(ProductViewModel model, CancellationToken cancellationToken)
    {
        var resultado = await _products.CreateAsync(_mapper.Map<ProductRequest>(model), cancellationToken);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = resultado.Value!.Id }, resultado.Value)
            : this.ProblemFor(resultado.Error!);
    }

    /// <summary>Actualiza un producto. Solo administradores.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, ProductViewModel model, CancellationToken cancellationToken)
        => this.OkOrProblem(await _products.UpdateAsync(id, _mapper.Map<ProductRequest>(model), cancellationToken));

    /// <summary>Elimina un producto. Solo administradores.</summary>
    /// <response code="204">Eliminado.</response>
    /// <response code="409">Tiene ventas registradas: desactívalo con PUT (isActive = false) en lugar de borrarlo.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => this.NoContentOrProblem(await _products.DeleteAsync(id, cancellationToken));
}
