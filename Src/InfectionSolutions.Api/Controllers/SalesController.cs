using AutoMapper;
using InfectionSolutions.Api.Common;
using InfectionSolutions.Api.ViewModels.Sales;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Notifications;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Application.Services.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Api.Controllers;

/// <summary>
/// Todas las ventas. Solo administradores; el cliente usa /api/me/sales.
/// Las ventas no se editan ni se borran: DELETE las anula y devuelve el stock.
/// </summary>
[ApiController]
[Route("api/sales")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Produces("application/json")]
public sealed class SalesController : ControllerBase
{
    private readonly ISaleService _sales;
    private readonly IReceiptService _receipts;
    private readonly ICustomerNotifier _notifier;
    private readonly IMapper _mapper;

    public SalesController(ISaleService sales, IReceiptService receipts, ICustomerNotifier notifier, IMapper mapper)
    {
        _sales = sales;
        _receipts = receipts;
        _notifier = notifier;
        _mapper = mapper;
    }

    /// <summary>Lista paginada. Busca por número de venta, nombre o documento del cliente.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<SaleListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SaleListItemResponse>>> List([FromQuery] SaleQuery query, CancellationToken cancellationToken)
        => Ok(await _sales.ListAsync(query, cancellationToken));

    /// <summary>Detalle con cliente, productos, subtotal, IVA y total.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => this.OkOrProblem(await _sales.GetByIdAsync(id, cancellationToken));

    /// <summary>Registra una venta: descuenta stock, calcula IVA, genera el recibo PDF y lo envía al cliente por correo.</summary>
    /// <response code="201">Venta registrada.</response>
    /// <response code="422">Stock insuficiente o producto inactivo.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SaleCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SaleCreatedResponse>> Create(SaleViewModel model, CancellationToken cancellationToken)
    {
        var resultado = await _sales.CreateAsync(_mapper.Map<SaleRequest>(model), User.UserId(), cancellationToken);
        if (resultado.IsFailure)
        {
            return this.ProblemFor(resultado.Error!);
        }

        var venta = resultado.Value!;
        var enviado = await _notifier.SendPurchaseConfirmationAsync(venta, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = venta.Id }, new SaleCreatedResponse(venta, enviado, venta.CustomerEmail));
    }

    /// <summary>Anula la venta (no se borra): devuelve el stock y regenera el recibo con el sello de anulada.</summary>
    /// <response code="204">Venta anulada.</response>
    /// <response code="422">La venta ya estaba anulada.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        => this.NoContentOrProblem(await _sales.CancelAsync(id, cancellationToken));

    /// <summary>Recibo PDF de la venta.</summary>
    [HttpGet("{id:guid}/receipt")]
    [Produces(ReceiptFile.ContentType, "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, ReceiptFile.ContentType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken cancellationToken)
    {
        var recibo = await _receipts.GetAsync(id, cancellationToken);

        return recibo.IsSuccess
            ? File(recibo.Value!.Content, ReceiptFile.ContentType, recibo.Value.FileName)
            : this.ProblemFor(recibo.Error!);
    }
}
