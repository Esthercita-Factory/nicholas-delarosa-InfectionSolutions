using AutoMapper;
using InfectionSolutions.Api.Common;
using InfectionSolutions.Api.ViewModels.Customers;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Application.Services.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Api.Controllers;

/// <summary>Gestión de clientes. Solo administradores (un cliente consulta su ficha en /api/auth/me).</summary>
[ApiController]
[Route("api/customers")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Produces("application/json")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;
    private readonly IMapper _mapper;

    public CustomersController(ICustomerService customers, IMapper mapper)
    {
        _customers = customers;
        _mapper = mapper;
    }

    /// <summary>Lista paginada con búsqueda por nombre, documento o correo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> List([FromQuery] CustomerQuery query, CancellationToken cancellationToken)
        => Ok(await _customers.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => this.OkOrProblem(await _customers.GetByIdAsync(id, cancellationToken));

    /// <response code="201">Cliente creado.</response>
    /// <response code="409">Documento o correo ya registrados.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(CustomerViewModel model, CancellationToken cancellationToken)
    {
        var resultado = await _customers.CreateAsync(_mapper.Map<CustomerRequest>(model), cancellationToken);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = resultado.Value!.Id }, resultado.Value)
            : this.ProblemFor(resultado.Error!);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Update(Guid id, CustomerViewModel model, CancellationToken cancellationToken)
        => this.OkOrProblem(await _customers.UpdateAsync(id, _mapper.Map<CustomerRequest>(model), cancellationToken));

    /// <response code="204">Eliminado.</response>
    /// <response code="409">Tiene ventas registradas: desactívalo con PUT (isActive = false).</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => this.NoContentOrProblem(await _customers.DeleteAsync(id, cancellationToken));
}
