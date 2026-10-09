using AutoMapper;
using InfectionSolutions.Api.Common;
using InfectionSolutions.Api.ViewModels.Sales;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Portal;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Portal;
using InfectionSolutions.Application.Services.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Api.Controllers;

/// <summary>
/// Compras del cliente autenticado (rol Cliente). El cliente se toma del token:
/// nunca de la petición, así que nadie puede ver ni comprar a nombre de otro.
/// </summary>
[ApiController]
[Route("api/me/sales")]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Produces("application/json")]
public sealed class MySalesController : ControllerBase
{
    private readonly ICustomerPortalService _portal;
    private readonly IMapper _mapper;

    public MySalesController(ICustomerPortalService portal, IMapper mapper)
    {
        _portal = portal;
        _mapper = mapper;
    }

    /// <summary>Historial de compras propias, de la más reciente a la más antigua.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<SaleListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SaleListItemResponse>>> List([FromQuery] SaleQuery query, CancellationToken cancellationToken)
        => this.OkOrProblem(await _portal.ListMySalesAsync(User.UserId(), query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => this.OkOrProblem(await _portal.GetMySaleAsync(User.UserId(), id, cancellationToken));

    /// <summary>
    /// Compra el carrito: valida stock, calcula subtotal, IVA (19 %) y total con los precios
    /// actuales, genera el comprobante PDF y lo envía al correo del cliente.
    /// </summary>
    /// <remarks>
    /// "receiptEmailSent" indica si el correo salió; si es false la compra igual quedó
    /// registrada y el comprobante se puede descargar en GET /api/me/sales/{id}/receipt.
    /// </remarks>
    /// <response code="201">Compra registrada.</response>
    /// <response code="403">Cuenta sin ficha de cliente o cliente inactivo.</response>
    /// <response code="422">Stock insuficiente o producto no disponible.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SaleCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SaleCreatedResponse>> Checkout(CheckoutViewModel model, CancellationToken cancellationToken)
    {
        var resultado = await _portal.CheckoutAsync(User.UserId(), _mapper.Map<CheckoutRequest>(model), cancellationToken);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = resultado.Value!.Sale.Id }, resultado.Value)
            : this.ProblemFor(resultado.Error!);
    }

    /// <summary>Descarga el comprobante PDF de una compra propia.</summary>
    [HttpGet("{id:guid}/receipt")]
    [Produces(ReceiptFile.ContentType, "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, ReceiptFile.ContentType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken cancellationToken)
    {
        var recibo = await _portal.GetMyReceiptAsync(User.UserId(), id, cancellationToken);

        return recibo.IsSuccess
            ? File(recibo.Value!.Content, ReceiptFile.ContentType, recibo.Value.FileName)
            : this.ProblemFor(recibo.Error!);
    }
}
